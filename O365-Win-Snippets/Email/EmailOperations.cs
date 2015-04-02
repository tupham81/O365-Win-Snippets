﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Office365.Discovery;
using Microsoft.Office365.OutlookServices;
using Microsoft.Office365.OutlookServices.Extensions;

namespace O365_Win_Snippets
{
    class EmailOperations
    {
        private static OutlookServicesClient _outlookClient = null;

        /// <summary>
        /// Checks that an OutlookServicesClient object is available. 
        /// </summary>
        /// <returns>The OutlookServicesClient object. </returns>
        public static async Task<OutlookServicesClient> GetOutlookClientAsync()
        {
            if (_outlookClient != null && !String.IsNullOrEmpty(AuthenticationHelper.LastAuthority))
            {
                Debug.WriteLine("Got an Outlook client for Mail.");
                return _outlookClient;
            }
            else
            {
                try
                {
                    //First, look for the authority used during the last authentication.
                    //If that value is not populated, use CommonAuthority.
                    string authority = null;

                    if (String.IsNullOrEmpty(AuthenticationHelper.LastAuthority))
                    {
                        authority = AuthenticationHelper.CommonAuthority;
                    }
                    else
                    {
                        authority = AuthenticationHelper.LastAuthority;
                    }

                    // Create an AuthenticationContext using this authority.
                    AuthenticationHelper._authenticationContext = new AuthenticationContext(authority);

                    // Set the value of _authenticationContext.UseCorporateNetwork to true so that you 
                    // can use this app inside a corporate intranet. If the value of UseCorporateNetwork 
                    // is true, you also need to add the Enterprise Authentication, Private Networks, and
                    // Shared User Certificates capabilities in the Package.appxmanifest file.
                    AuthenticationHelper._authenticationContext.UseCorporateNetwork = true;

                    //See the Discovery Service Sample (https://github.com/OfficeDev/Office365-Discovery-Service-Sample)
                    //for an approach that improves performance by storing the discovery service information in a cache.
                    DiscoveryClient discoveryClient = new DiscoveryClient(
                        async () => await AuthenticationHelper.GetTokenHelperAsync(AuthenticationHelper._authenticationContext, AuthenticationHelper.DiscoveryResourceId));

                    // Get the specified capability ("Calendar").
                    CapabilityDiscoveryResult result =
                        await discoveryClient.DiscoverCapabilityAsync("Mail");

                    var token = await AuthenticationHelper.GetTokenHelperAsync(AuthenticationHelper._authenticationContext, result.ServiceResourceId);
                    // Check the token
                    if (String.IsNullOrEmpty(token))
                    {
                        // User cancelled sign-in
                        return null;
                    }
                    else
                    {

                        _outlookClient = new OutlookServicesClient(
                            result.ServiceEndpointUri,
                            async () => await AuthenticationHelper.GetTokenHelperAsync(AuthenticationHelper._authenticationContext, result.ServiceResourceId));
                        Debug.WriteLine("Got an Outlook client for Mail.");
                        return _outlookClient;
                    }
                }
                // The following is a list of exceptions you should consider handling in your app.
                // In the case of this sample, the exceptions are handled by returning null upstream. 
                catch (DiscoveryFailedException dfe)
                {
                    Debug.WriteLine(dfe.Message);
                }
                catch (ArgumentException ae)
                {
                    Debug.WriteLine(ae.Message);
                }

                AuthenticationHelper._authenticationContext.TokenCache.Clear();

                return null;
            }
        }

        public static async Task<List<IMessage>> GetInboxMessagesAsync()
        {
            try
            {
                // Make sure we have a reference to the Exchange client
                OutlookServicesClient outlookClient = await GetOutlookClientAsync();

                //Get messages from a specific folder. Using Inbox this time because it is most likely to be populated.
                var results = await outlookClient.Me.Folders.GetById("Inbox").Messages.ExecuteAsync();

                if (results.CurrentPage.Count > 0)
                {
                    Debug.WriteLine("First message from Inbox folder:" + results.CurrentPage[0].Id);
                }

                return results.CurrentPage.ToList();
            }
            catch { return null; }
        }


        public static async Task<List<IMessage>> GetMessagesAsync()
        {

            try
            {
                // Make sure we have a reference to the Exchange client
                var outlookClient = await GetOutlookClientAsync();

                //Get messages (from Inbox by default)
                var results = await outlookClient.Me.Messages.ExecuteAsync();

                if (results.CurrentPage.Count > 0)
                {
                    Debug.WriteLine("First message from Inbox folder:" + results.CurrentPage[0].Id);
                }

                return results.CurrentPage.ToList();
            }
            catch { return null; }
        }

        public static async Task<IMessage> GetMessagesAsync(string subject, DateTimeOffset after)
        {

            try
            {
                // Make sure we have a reference to the Exchange client
                var outlookClient = await GetOutlookClientAsync();

                //Get messages (from Inbox by default).
                // Note: This query is not guaranteed to return 0 or 1 message, so 
                // I need to use ExecuteAsync. Otherwise, ExecuteSingleAsync will throw
                // InvalidOperationException.
                var result = await outlookClient.Me.Messages
                            .Where(m => m.Subject == subject && m.DateTimeReceived > after)
                            .ExecuteAsync();


                return (result.CurrentPage.Count > 0) ? result.CurrentPage[0] : null;
            }
            catch { return null; }
        }

        public static async Task<bool> SendMessageAsync(
            string Subject,
            string Body,
            string RecipientAddress
            )
        {
            try
            {
                // Make sure we have a reference to the Outlook Services client
                var outlookClient = await GetOutlookClientAsync();

                //Create Body
                ItemBody body = new ItemBody
                {
                    Content = Body,
                    ContentType = BodyType.HTML
                };
                List<Recipient> toRecipients = new List<Recipient>();
                toRecipients.Add(new Recipient
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = RecipientAddress
                    }
                });

                Message newMessage = new Message
                {
                    Subject = Subject,
                    Body = body,
                    ToRecipients = toRecipients
                };

                // To send a message without saving to Sent Items, specify false for  
                // the SavetoSentItems parameter. 
                await outlookClient.Me.SendMailAsync(newMessage, true);

                Debug.WriteLine("Sent mail: " + newMessage.Id);

                return true;
            }
            catch { return false; }
        }

        public static async Task<string> CreateDraftAsync(
            string Subject,
            string Body,
            string RecipientAddress)
        {

            try
            {
                // Make sure we have a reference to the Outlook Services client
                OutlookServicesClient outlookClient = await GetOutlookClientAsync();

                ItemBody body = new ItemBody
                {
                    Content = Body,
                    ContentType = BodyType.HTML
                };
                List<Recipient> toRecipients = new List<Recipient>();
                toRecipients.Add(new Recipient
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = RecipientAddress
                    }
                });
                Message draftMessage = new Message
                {
                    Subject = Subject,
                    Body = body,
                    ToRecipients = toRecipients,
                    Importance = Importance.High
                };

                // Save the draft message. Saving to Me.Messages saves the message in the Drafts folder.
                await outlookClient.Me.Messages.AddMessageAsync(draftMessage);

                Debug.WriteLine("Created draft: " + draftMessage.Id);

                return draftMessage.Id;
            }
            catch { return null; }
        }

        public static async Task<string> CreateDraftAndSendAsync(
            string Subject,
            string Body,
            string RecipientAddress)
        {

            try
            {
                // Make sure we have a reference to the Outlook Services client
                OutlookServicesClient outlookClient = await GetOutlookClientAsync();

                ItemBody body = new ItemBody
                {
                    Content = Body,
                    ContentType = BodyType.HTML
                };
                List<Recipient> toRecipients = new List<Recipient>();
                toRecipients.Add(new Recipient
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = RecipientAddress
                    }
                });
                Message draftMessage = new Message
                {
                    Subject = Subject,
                    Body = body,
                    ToRecipients = toRecipients,
                    Importance = Importance.High
                };

                // Save the draft message. This ensures that we'll get a message Id to return.
                await outlookClient.Me.Messages.AddMessageAsync(draftMessage);

                //Send the message.

                await outlookClient.Me.Messages[draftMessage.Id].SendAsync();

                Debug.WriteLine("Created and sent draft: " + draftMessage.Id);

                return draftMessage.Id;
            }
            catch { return null; }
        }

        public static async Task<bool> UpdateMessageAsync(string MessageId, string UpdatedContent)
        {
            try
            {
                // Make sure we have a reference to the Outlook Services client
                OutlookServicesClient outlookClient = await GetOutlookClientAsync();

                // Get the message to update and set changed properties
                IMessage message = await outlookClient.Me.Messages[MessageId].ExecuteAsync();
                message.Body = new ItemBody
                {
                    Content = UpdatedContent,
                    ContentType = BodyType.HTML
                };

                await message.UpdateAsync();

                Debug.WriteLine("Updated message: " + message.Id);

                return true;
            }
            catch { return false; }
        }

        public static async Task<bool> ReplyMessageAsync(string MessageId, string ReplyContent)
        {
            try
            {
                // Make sure we have a reference to the Outlook Services client
                OutlookServicesClient outlookClient = await GetOutlookClientAsync();

                IMessage message = await outlookClient.Me.Messages[MessageId].ExecuteAsync();
                await message.ReplyAsync(ReplyContent);

                Debug.WriteLine("Replied to message: " + message.Id);

                return true;
            }
            catch { return false; }
        }

        public static async Task<bool> ReplyAllAsync(string MessageId, string ReplyContent)
        {
            try
            {
                // Make sure we have a reference to the Outlook Services client
                OutlookServicesClient outlookClient = await GetOutlookClientAsync();

                IMessage message = await outlookClient.Me.Messages[MessageId].ExecuteAsync();
                await message.ReplyAllAsync(ReplyContent);

                Debug.WriteLine("Replied all to message: " + message.Id);


                return true;
            }
            catch { return false; }
        }

        public static async Task<bool> ForwardMessageAsync(
            string MessageId, 
            string ForwardMessage, 
            string RecipientAddress)
        {

            try
            {
                // Make sure we have a reference to the Outlook Services client
                OutlookServicesClient outlookClient = await GetOutlookClientAsync();

                List<Recipient> toRecipients = new List<Recipient>();
                toRecipients.Add(new Recipient
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = RecipientAddress
                    }
                });

                await outlookClient.Me.Messages[MessageId].ForwardAsync(ForwardMessage, toRecipients);

                Debug.WriteLine("Forwarded message: " + MessageId);

                return true;
            }
            catch { return false; }
        }


        public static async Task<bool> MoveMessageAsync(string MessageId, string OriginalFolder, string ToFolder)
        {
            try
            {
                // Make sure we have a reference to the Outlook Services client
                OutlookServicesClient outlookClient = await GetOutlookClientAsync();

                IMessage messageToMove = await outlookClient.Me.Messages[MessageId].ExecuteAsync();
                IMessage movedMessage = await messageToMove.MoveAsync(ToFolder);

                Debug.WriteLine("Moved message: " + MessageId + " from " + OriginalFolder + " to " + ToFolder);

                return true;
            }
            catch { return false; }
        }

        public static async Task<bool> CopyMessageAsync(string MessageId, string OriginalFolder, string ToFolder)
        {
            try
            {
                // Make sure we have a reference to the Outlook Services client
                OutlookServicesClient outlookClient = await GetOutlookClientAsync();

                IMessage messageToMove = await outlookClient.Me.Messages[MessageId].ExecuteAsync();
                IMessage movedMessage = await messageToMove.CopyAsync(ToFolder);

                Debug.WriteLine("Copied message: " + MessageId + " from " + OriginalFolder + " to " + ToFolder);

                return true;
            }
            catch { return false; }
        }

        public static async Task<bool> DeleteMessageAsync(string MessageId)
        {
            try
            {
                // Make sure we have a reference to the Outlook Services client
                OutlookServicesClient outlookClient = await GetOutlookClientAsync();

                // Get the message to delete.
                IMessage message = await outlookClient.Me.Messages[MessageId].ExecuteAsync();
                await message.DeleteAsync();

                Debug.WriteLine("Deleted message: " + MessageId)
;
                return true;
            }
            catch { return false; }
        }

        //Mail Folders operations

        public static async Task<IPagedCollection<IFolder>> GetMailFoldersAsync()
        {
            try
            {
                OutlookServicesClient outlookClient = await GetOutlookClientAsync();

                IPagedCollection<IFolder> foldersResults = await outlookClient.Me.Folders.ExecuteAsync();

                string folderId = foldersResults.CurrentPage[0].Id;

                if (string.IsNullOrEmpty(folderId)) return null;

                Debug.WriteLine("First mail folder in the collection: " + folderId);

                return foldersResults;
            }
            catch { return null; }
        }

        public static async Task<string> CreateMailFolderAsync(string ParentFolder, string NewFolderName)
        {
            try
            {
                OutlookServicesClient outlookClient = await GetOutlookClientAsync();

                Folder newFolder = new Folder
                {
                    DisplayName = NewFolderName
                };
                await outlookClient.Me.Folders[ParentFolder].ChildFolders.AddFolderAsync(newFolder);

                Debug.WriteLine("Created folder: " + newFolder.Id);

                // Get the ID of the new folder.
                return newFolder.Id;

            }
            catch { return null; }
        }

        public static async Task<bool> UpdateMailFolderAsync(string FolderId, string NewFolderName)
        {
            try
            {
                OutlookServicesClient outlookClient = await GetOutlookClientAsync();

                IFolder folder = await outlookClient.Me.Folders[FolderId].ExecuteAsync();
                folder.DisplayName = NewFolderName;
                await folder.UpdateAsync();
                string updatedName = folder.DisplayName;

                Debug.WriteLine("Updated folder name: " + FolderId + " " + NewFolderName);

                return true;
            }
            catch { return false; }
        }

        public static async Task<bool> MoveMailFolderAsync(string folderId, string ToFolderName)
        {
            try
            {
                OutlookServicesClient outlookClient = await GetOutlookClientAsync();

                IFolder folderToMove = await outlookClient.Me.Folders[folderId].ExecuteAsync();
                await folderToMove.MoveAsync(ToFolderName);
                Debug.WriteLine("Moved folder: " + folderId + " to " + ToFolderName);

                return true;
            }
            catch { return false; }
        }

        public static async Task<string> CopyMailFolderAsync(string folderId, string ToFolderName)
        {
            try
            {
                OutlookServicesClient outlookClient = await GetOutlookClientAsync();

                IFolder folderToCopy = await outlookClient.Me.Folders[folderId].ExecuteAsync();
                IFolder copiedFolder = await folderToCopy.CopyAsync(ToFolderName);

                Debug.WriteLine("Copied folder: " + folderId + " to " + ToFolderName);

                return copiedFolder.Id;
            }
            catch { return null; }
        }


        public static  async Task<bool> DeleteMailFolderAsync(string folderId)
        {
            try
            {
                OutlookServicesClient outlookClient = await GetOutlookClientAsync();

                IFolder folder = await outlookClient.Me.Folders[folderId].ExecuteAsync();
                await folder.DeleteAsync();

                Debug.WriteLine("Deleted folder: " + folderId);

                return true;
            }
            catch { return false; }
        }

    }
}