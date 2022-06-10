// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with EmptyBot .NET Template version v4.15.2

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
// using System.Windows.Forms;
using Microsoft.Data.SqlClient;


using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.TraceExtensions;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Logging;

namespace EmptyBot
{
    public class AdapterWithErrorHandler : CloudAdapter
    {

        static string connetionString = null;
        static SqlConnection connection;
        static SqlCommand command;
        static string sqlBase = "INSERT INTO BotActivity (activityId, userId, userName, userMessage, "
        +"botId, botName, botMessage, activityTime) "
        +"VALUES "
        +"('{0}', '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}');";
        
        static SqlDataReader dataReader;

        // This is for connecting to local database.
        // static SqlConnectionStringBuilder sqlBuilder = new SqlConnectionStringBuilder
        // {
        //     DataSource = "localhost",
        //     InitialCatalog = "TestDB",
        //     Password = "Noneatall@1234",
        //     UserID = "SA",
        //     TrustServerCertificate = true
        // };

        static SqlConnectionStringBuilder sqlBuilder = new SqlConnectionStringBuilder
        {
            DataSource = "tcc-test-db-server.database.windows.net",
            InitialCatalog = "TestDB",
            Password = "nopassword!1",
            UserID = "tcc-admin1",
            TrustServerCertificate = true
        };


        public AdapterWithErrorHandler(BotFrameworkAuthentication auth, ILogger<IBotFrameworkHttpAdapter> logger)
            : base(auth, logger)
        {
            OnTurnError = async (turnContext, exception) =>
            {
                // Log any leaked exception from the application.
                logger.LogError(exception, $"[OnTurnError] unhandled error : {exception.Message}");

                // Send a message to the user
                await turnContext.SendActivityAsync("The bot encountered an error or bug.");
                await turnContext.SendActivityAsync("To continue to run this bot, please fix the bot source code.");

                // Send a trace activity, which will be displayed in the Bot Framework Emulator
                await turnContext.TraceActivityAsync("OnTurnError Trace", exception.Message, "https://www.botframework.com/schemas/error", "TurnError");
            };

            connetionString = sqlBuilder.ToString();
            connection = new SqlConnection(connetionString);      
        }

        public async Task ProcessActivityAsync(BotCallbackHandler callback = null)
        {
            while (true)
            {
                var msg = Console.ReadLine();
                if (msg == null)
                {
                    break;
                }

                // Performing the conversion from console text to an Activity for
                // which the system handles all messages (from all unique services).
                // All processing is performed by the broader bot pipeline on the Activity
                // object.
                var activity = new Activity()
                {
                    Text = msg,

                    // Note on ChannelId:
                    // The Bot Framework channel is identified by a unique ID.
                    // For example, "skype" is a common channel to represent the Skype service.
                    // We are inventing a new channel here.
                    ChannelId = "console",
                    From = new ChannelAccount(id: "user", name: "User1"),
                    Recipient = new ChannelAccount(id: "bot", name: "Bot"),
                    Conversation = new ConversationAccount(id: "Convo1"),
                    // Timestamp = DateTime.UtcNow,
                    Timestamp = DateTime.Now,
                    Id = Guid.NewGuid().ToString(),
                    Type = ActivityTypes.Message,
                };


                using (var context = new TurnContext(this, activity))
                {
                    await this.RunPipelineAsync(context, callback, default(CancellationToken)).ConfigureAwait(false);
                }
            }
        }

        // Sends activities to the conversation.
        public override async Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext context, Activity[] activities, CancellationToken cancellationToken)
        {

            await base.SendActivitiesAsync(context, activities, cancellationToken);
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (activities == null)
            {
                throw new ArgumentNullException(nameof(activities));
            }

            if (activities.Length == 0)
            {
                throw new ArgumentException("Expecting one or more activities, but the array was empty.", nameof(activities));
            }


            var conAct = context.Activity;
            // Console.WriteLine($"\n\n {conAct.From.Name}");
            // Console.WriteLine($"{conAct.Timestamp} \n\n");

            var responses = new ResourceResponse[activities.Length];

            for (var index = 0; index < activities.Length; index++)
            {
                var activity = activities[index];

                switch (activity.Type)
                {
                    case ActivityTypes.Message:
                        {
                            IMessageActivity message = activity.AsMessageActivity();
                            ChannelAccount acc = message.From;
                            // Console.WriteLine($"\n\n {acc.Properties} \n\n");

                            // A message exchange between user and bot can contain media attachments
                            // (e.g., image, video, audio, file).  In this particular example, we are unable
                            // to create Attachments to messages, but this illustrates processing.
                            if (message.Attachments != null && message.Attachments.Any())
                            {
                                var attachment = message.Attachments.Count == 1 ? "1 attachment" : $"{message.Attachments.Count()} attachments";
                                Console.WriteLine($"{message.Text} with {attachment} ");
                            }
                            else
                            {
                                // {conAct.Timestamp}
                                Console.WriteLine($"\n{conAct.Id}");
                                Console.WriteLine($"{conAct.From.Id}, {conAct.From.Name}: {conAct.Text}");
                                Console.WriteLine($"{conAct.Recipient.Id}, {conAct.Recipient.Name}: {message.Text}");
                                Console.WriteLine($"{conAct.Timestamp}\n");

                                dbInsert(conAct.Id, conAct.From.Id, conAct.From.Name, conAct.Text,
                                        conAct.Recipient.Id, conAct.Recipient.Name, message.Text, DateTime.Now.ToString());

                            }
                        }

                        break;

                    case ActivityTypesEx.Delay:
                        {
                            // The Activity Schema doesn't have a delay type build in, so it's simulated
                            // here in the Bot. This matches the behavior in the Node connector.
                            int delayMs = (int)((Activity)activity).Value;
                            await Task.Delay(delayMs).ConfigureAwait(false);
                        }

                        break;

                    case ActivityTypes.Trace:
                        // Do not send trace activities unless you know that the client needs them.
                        // For example: BF protocol only sends Trace Activity when talking to emulator channel.
                        break;

                    default:
                        Console.WriteLine("Bot: activity type: {0}", activity.Type);
                        break;
                }

                responses[index] = new ResourceResponse(activity.Id);
            }

            return responses;
        }


        // Insert the messaging activity into the sql server database.
        private void dbInsert(string activityId, string userId, string userName, string userMessage,
                string botId, string botName, string botMessage, string activityTime)
        {
            string sql = String.Format(sqlBase, activityId, userId, userName, userMessage,
                    botId, botName, botMessage, activityTime);

            Console.WriteLine(sql);
            try
            {
                connection.Open();
                command = new SqlCommand(sql, connection);

                dataReader = command.ExecuteReader();
                dataReader.Close();

                command.Dispose();
                connection.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

    }
}
