﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.WindowsAzure.ActiveDirectory.GraphHelper;
using Neo4jClient;

namespace OrgChart.Models
{
    // for the neo4j graph database calls
    public class User
    {
        public String mailNickname { get; set; }
        public String trioLed { get; set; }
        public String linkedInUrl { get; set; }
    }

    public class AadExtendedUser : AadUser
    {
        public AadExtendedUser(AadUser user, String trio, String liurl)
        {
            accountEnabled = user.accountEnabled;
            assignedLicenses = user.assignedLicenses;
            //assignedPlans = user.assignedPlans; // no set provided
            otherMails = user.otherMails;
            passwordPolicies = user.passwordPolicies;
            passwordProfile = user.passwordProfile;
            preferredLanguage = user.preferredLanguage;
            //provisionedPlans = user.provisionedPlans; // no set provided
            usageLocation = user.usageLocation;
            userPrincipalName = user.userPrincipalName;
            city = user.city;
            country = user.country;
            department = user.department;
            dirSyncEnabled = user.dirSyncEnabled;
            displayName = user.displayName;
            facsimileTelephoneNumber = user.facsimileTelephoneNumber;
            givenName = user.givenName;
            jobTitle = user.jobTitle;
            lastDirSyncTime = user.lastDirSyncTime;
            mail = user.mail;
            mailNickname = user.mailNickname;
            mobile = user.mobile;
            objectId = user.objectId;
            objectType = user.objectType;
            ODataType = user.ODataType;
            physicalDeliveryOfficeName = user.physicalDeliveryOfficeName;
            postalCode = user.postalCode;
            provisioningErrors = user.provisioningErrors;
            //proxyAddresses = user.proxyAddresses; // no set provided
            state = user.state;
            streetAddress = user.streetAddress;
            surname = user.surname;
            telephoneNumber = user.telephoneNumber;
            trioLed = trio;
            linkedInUrl = liurl;
            isManager = false;
        }
        public string trioLed { get; set; }
        public string linkedInUrl { get; set; }
        public bool isManager { get; set; }
    }

    public class Org
    {
        private GraphQuery graphCall;
        private GraphClient neo4jClient;
        public Org(GraphQuery gq, GraphClient gc)
        {
            graphCall = gq;
            neo4jClient = gc;
        }
        public AadUser createUser(string strCreateUPN, string strCreateMailNickname, string strCreateDisplayName, string strCreateManagerUPN, string strCreateJobTitle)
        {
            string strTrioLed = "";
            string strLinkedInUrl = "";
            AadUser user = new AadUser();
            user.userPrincipalName = strCreateUPN;
            user.displayName = strCreateDisplayName;
            user.mailNickname = strCreateMailNickname;
            user.jobTitle = strCreateJobTitle;
            user.passwordProfile = new passwordProfile();
            user.passwordProfile.forceChangePasswordNextLogin = true;
            user.passwordProfile.password = "P0rsche911";
            AadUser newUser = graphCall.createUser(user);
            if (newUser != null)
            {
                newUser = setUser(strCreateUPN, strCreateDisplayName, strCreateManagerUPN, strCreateJobTitle, strTrioLed, strLinkedInUrl);
            }
            return newUser;
        }
        public void deleteUser(string strUpdateUPN)
        {
            // set new (or same) display name
            AadUser user = graphCall.getUser(strUpdateUPN);
            bool bPass = graphCall.modifyUser("DELETE", user);
        }
        // list with main person as the last item in list
        public List<AadExtendedUser> getAncestorsAndMainPerson(string strUPN)
        {
            List<AadExtendedUser> returnedList = new List<AadExtendedUser>();
            AadUser graphUser = graphCall.getUser(strUPN);
            while (graphUser != null)
            {
                // retrieve corresponding neo4j object
                var results = neo4jClient.Cypher
                    .Match("(user:User)")
                    .Where((User user) => user.mailNickname == graphUser.mailNickname)
                    .Return(user => user.As<User>())
                    .Results;
                // retrieve trio and url from neo4j object
                String trioLed = "", linkedInUrl = "";
                foreach (User user in results)
                {
                    trioLed = user.trioLed;
                    linkedInUrl = user.linkedInUrl;
                }
                // insert the direct report at front of a ancestor list
                AadExtendedUser extendedUser = new AadExtendedUser(graphUser, trioLed, linkedInUrl);
                returnedList.Insert(0, extendedUser);
                graphUser = graphCall.getUsersManager(graphUser.userPrincipalName);
            }
            return returnedList;
        }
        // list with ICs as single person lists and leads as multiple person lists
        public List<List<AadExtendedUser>> getDirectsOfDirects(string strUPN)
        {
            List<List<AadExtendedUser>> returnedListOfLists = new List<List<AadExtendedUser>>();
            AadUsers directs = graphCall.getUsersDirectReports(strUPN);
            if (directs != null)
            {
                foreach (AadUser directReport in directs.user)
                {
                    // retrieve corresponding neo4j object
                    var results = neo4jClient.Cypher
                        .Match("(user:User)")
                        .Where((User user) => user.mailNickname == directReport.mailNickname)
                        .Return(user => user.As<User>())
                        .Results;
                    // retrieve trio and url from neo4j object
                    String trioLed = "", linkedInUrl = "";
                    foreach (User user in results)
                    {
                        trioLed = user.trioLed;
                        linkedInUrl = user.linkedInUrl;
                    }
                    // add a new list at start of list of lists
                    returnedListOfLists.Insert(0, new List<AadExtendedUser>());
                    // insert the direct report at front of newly inserted list
                    AadExtendedUser extendedDirectReport = new AadExtendedUser(directReport, trioLed, linkedInUrl);
                    returnedListOfLists.ElementAt(0).Insert(0, extendedDirectReport);
                    // get direct reports of the direct report
                    AadUsers directsOfDirects = graphCall.getUsersDirectReports(directReport.userPrincipalName);
                    extendedDirectReport.isManager = (directsOfDirects.user.Count > 0 ? true : false);
                    foreach (AadUser directOfDirect in directsOfDirects.user)
                    {
                        // retrieve corresponding neo4j object
                        results = neo4jClient.Cypher
                            .Match("(user:User)")
                            .Where((User user) => user.mailNickname == directOfDirect.mailNickname)
                            .Return(user => user.As<User>())
                            .Results;
                        // retrieve trio and url from neo4j object
                        foreach (User user in results)
                        {
                            trioLed = user.trioLed;
                            linkedInUrl = user.linkedInUrl;
                        }
                        // add each direct of direct to the list
                        AadExtendedUser extendedDirectOfDirect = new AadExtendedUser(directOfDirect, trioLed, linkedInUrl);
                        returnedListOfLists.ElementAt(0).Add(extendedDirectOfDirect);
                    }
                }
            }
            return returnedListOfLists;
        }
        public string getFirstUpn(bool bUpdateCache)
        {
            string userPrincipalName = null;
            AadUsers users = graphCall.getUsers();
            if (users != null)
            {
                userPrincipalName = users.user[0].userPrincipalName;
            }
            if (bUpdateCache) // called at app start and just got all the users...
            {
                // iterate over all users to load into neo4j
                foreach (AadUser user in users.user)
                {
                    // declare new user object
                    var newUser = new User { mailNickname = user.mailNickname, trioLed = "", linkedInUrl = ""};
                    // MERGE doesn't support map properties, need to explicitly specify properties
                    string strMerge = @"(user:User { mailNickname: {newUser}.mailNickname, 
                                                     trioLed: {newUser}.trioLed, 
                                                     linkedInUrl: {newUser}.linkedInUrl
                                                   })";
                    // neo4j call to store user
                    neo4jClient.Cypher
                            .Merge(strMerge)
                            .WithParam("newUser", newUser)
                            .ExecuteWithoutResults();
                }
                // iterate again to create :MANAGES links
                foreach (AadUser user in users.user)
                {
                    //set WHERE string for this user
                    String strWhere1 = "u.mailNickname = \"";
                    strWhere1 += user.mailNickname;
                    strWhere1 += "\"";
                    String strMatch2;
                    String strWhere2;
                    String strLinkCreation;

                    // graph call to get manager
                    AadUser manager = graphCall.getUsersManager(user.userPrincipalName);

                    // set strings for node that will point to this user
                    if (manager != null)
                    {
                        strMatch2 = "(m:User)";
                        strWhere2 = "m.mailNickname = \"";
                        strWhere2 += manager.mailNickname;
                        strWhere2 += "\"";
                        strLinkCreation = "m-[:MANAGES]->u";
                    }
                    else
                    {
                        strMatch2 = "(m)";
                        strWhere2 = "NOT (m:User)";
                        strLinkCreation = "m-[:CONTAINS]->u";
                    }
                    // neo4j call to set :MANAGES or :CONTAINS link
                    neo4jClient.Cypher
                        .Match("(u:User)", strMatch2)
                        .Where(strWhere1)
                        .AndWhere(strWhere2)
                        .CreateUnique(strLinkCreation)
                        .ExecuteWithoutResults();
                }
            }
            return userPrincipalName;
        }
        public AadUser setUser(string strUpdateUPN, string strUpdateDisplayName, string strUpdateManagerUPN, string strUpdateJobTitle, string strUpdateTrioLed, string strUpdateLinkedInUrl)
        {
            // set new (or same) display name and job title
            AadUser graphUser = graphCall.getUser(strUpdateUPN);
            graphUser.displayName = strUpdateDisplayName;
            graphUser.jobTitle = strUpdateJobTitle;
            bool bPass = graphCall.modifyUser("PATCH", graphUser);
            // set new (or same) manager if a valid manager
            if (strUpdateManagerUPN != "NO MANAGER")
            {
                string updateManagerURI = graphCall.baseGraphUri + "/users/" + strUpdateUPN + "/$links/manager?" + graphCall.apiVersion;
                AadUser manager = graphCall.getUser(strUpdateManagerUPN);
                urlLink managerlink = new urlLink();
                managerlink.url = graphCall.baseGraphUri + "/directoryObjects/" + manager.objectId;
                bPass = (bPass && graphCall.updateLink(updateManagerURI, "PUT", managerlink));
            }
            // set extension data in neo4j
            var neo4jUser = new User { mailNickname = graphUser.mailNickname, trioLed = strUpdateTrioLed, linkedInUrl = strUpdateLinkedInUrl };
            // MERGE doesn't support map properties, need to explicitly specify properties
            string strMerge = @"(user:User { mailNickname: {neo4jUser}.mailNickname, 
                                                     trioLed: {neo4jUser}.trioLed, 
                                                     linkedInUrl: {neo4jUser}.linkedInUrl
                                                   })";
            // neo4j call to store user
            neo4jClient.Cypher
                    .Merge(strMerge)
                    .WithParam("neo4jUser", neo4jUser)
                    .ExecuteWithoutResults();

            return graphUser;
        }
        
        // TODO: figure out how to implement observer pattern - publish subscribe mechanism, for differential query
		
        // these can only be done with a cache
        //public List<AadUser> matchPartialAlias(string strPartial); // (fast full-text lookups)
		//public AadUser cacheAADGraph(string strUPN); // (makes 2 passes to load kids and grandkids, return requested AADUser)
		//public AadUser cacheLinkedIn(string strUPN); // (load connections, schools, employers)
		//public List<AadUser> findShortestPath(); // (connections)
        //public List<AadUser> findSharedHistory(); // (schools, employers)
    }
}