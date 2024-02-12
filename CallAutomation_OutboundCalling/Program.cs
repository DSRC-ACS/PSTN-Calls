using Azure;
using Azure.Communication;
using Azure.Communication.CallAutomation;
using Azure.Core;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using CallAutomation_OutboundCalling;

using Microsoft.CognitiveServices.Speech.Audio;

using static CallAutomation_OutboundCalling.Model;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>{    options.AddPolicy("AllowAll", builder =>    {        builder.AllowAnyOrigin()               .AllowAnyMethod()               .AllowAnyHeader();    });});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Your ACS resource connection string
var acsConnectionString = "";

// Your ACS resource phone number will act as source number to start outbound call
var acsPhonenumber = "";

// Target phone number you want to receive the call.
var targetPhonenumber = "";

// Base url of the app
var callbackUriHost = "";  //ngrok url or app services url

var cognitiveServiceEndpoint = "";





// text to play
const string SpeechToTextVoice = "en-US-ElizabethNeural";
const string MainMenu =
    """ 
    Hello this is Contoso Bank, we’re calling in regard to your appointment tomorrow 
    at 9am to open a new account. Please say confirm if this time is still suitable for you or say cancel 
    if you would like to cancel this appointment.
    """;
const string ConfirmedText = "Thank you for confirming your appointment tomorrow at 9am, we look forward to meeting with you.";
const string CancelText = """
Your appointment tomorrow at 9am has been cancelled. Please call the bank directly 
if you would like to rebook for another date and time.
""";
const string CustomerQueryTimeout = "I’m sorry I didn’t receive a response, please try again.";
const string NoResponse = "I didn't receive an input, we will go ahead and confirm your appointment. Goodbye";
const string InvalidAudio = "I’m sorry, I didn’t understand your response, please try again.";
const string ConfirmChoiceLabel = "Confirm";
const string RetryContext = "retry";

CallAutomationClient callAutomationClient = new CallAutomationClient(acsConnectionString);


var app = builder.Build();

app.MapPost("/outboundCall", async (string Phonenumber, ILogger<Program> logger) =>
{
    var key=(Environment.GetEnvironmentVariable("b74d3680dd3349918f0b0cbaba69ab26"));
    var region=(Environment.GetEnvironmentVariable("eastus"));

    PhoneNumberIdentifier target = new PhoneNumberIdentifier(Phonenumber);
    PhoneNumberIdentifier caller = new PhoneNumberIdentifier(acsPhonenumber);
    var callbackUri = new Uri(new Uri(callbackUriHost), "/api/callbacks");
    CallInvite callInvite = new CallInvite(target, caller);;
    var createCallOptions = new CreateCallOptions(callInvite, callbackUri)
    {
        CallIntelligenceOptions = new CallIntelligenceOptions() { CognitiveServicesEndpoint = new Uri(cognitiveServiceEndpoint)}
    };

    CreateCallResult createCallResult = await callAutomationClient.CreateCallAsync(createCallOptions);



    logger.LogInformation($"Created call with connection id: {createCallResult.CallConnectionProperties.CallConnectionId}");

    return createCallResult;
});

app.MapPost("/api/callbacks", async (CloudEvent[] cloudEvents, ILogger<Program> logger) =>
{



    foreach (var cloudEvent in cloudEvents)
    {
        CallAutomationEventBase parsedEvent = CallAutomationEventParser.Parse(cloudEvent);
        logger.LogInformation(
                    "Received call event: {type}, callConnectionID: {connId}, serverCallId: {serverId}",
                    parsedEvent.GetType(),
                    parsedEvent.CallConnectionId,
                    parsedEvent.ServerCallId);

        var callConnection = callAutomationClient.GetCallConnection(parsedEvent.CallConnectionId);
        var callMedia = callConnection.GetCallMedia();

        if (parsedEvent is ParticipantsUpdated _participantsUpdated)
        {

            CallProcess.participant_id = _participantsUpdated.Participants[0].Identifier;
        }

        
        if (parsedEvent is CallConnected callConnected)
        {


            var servercallId= callAutomationClient.GetCallConnection(parsedEvent.CallConnectionId).GetCallConnectionProperties().Value.ServerCallId;
            Console.WriteLine("ServerCallID : "+servercallId);

            //Once Start Recording is uncommented, code behaves differently

            //StartRecordingOptions recordingOptions = new StartRecordingOptions(new ServerCallLocator(servercallId))
            //{
            //    RecordingContent = RecordingContent.Audio,
            //    RecordingChannel = RecordingChannel.Mixed,
            //    RecordingFormat = RecordingFormat.Wav,
            //    RecordingStateCallbackUri = new Uri(callbackUriHost+"/callbacks")
            //};
            //Response<RecordingStateResult> response = await callAutomationClient.GetCallRecording()
            //.StartAsync(recordingOptions);

            await CallProcess.RecordResponse(callAutomationClient,parsedEvent, "Hi, we are calling from contoso bank. Please share last for digits of your credit card number.");
            
        }
        else if (parsedEvent is RecognizeCompleted recognizeCompleted)
        {

            switch (recognizeCompleted.RecognizeResult)
            {
                case SpeechResult speechResult:
                    var text = speechResult.Speech;
                    if (text.Contains("1234"))
                    {
                        await CallProcess.PlayPrompt(callAutomationClient, parsedEvent, "Thankyou for the response.");
                    }
                    else
                    {
                        await CallProcess.RecordResponse(callAutomationClient, parsedEvent, "Please share the last for digits of your credit card again");
                    }
                    logger.LogInformation("Recognize completed succesfully, text={text}", text);
                    break;
                default:
                    logger.LogInformation("Recognize completed succesfully, recognizeResult={recognizeResult}", recognizeCompleted.RecognizeResult);
                    break;
            }

        }
    }
    return Results.Ok();
}).Produces(StatusCodes.Status200OK);


app.MapPost("/api/DownloadRecording", async ( object request, ILogger<Program> logger) =>{    try
    {
        var httpContent = new BinaryData(request.ToString()).ToStream();
        EventGridEvent cloudEvent = EventGridEvent.ParseMany(BinaryData.FromStream(httpContent)).FirstOrDefault();

        if (cloudEvent.EventType == SystemEventNames.EventGridSubscriptionValidation)
        {
            var eventData = cloudEvent.Data.ToObjectFromJson<SubscriptionValidationEventData>();

            logger.LogInformation("Microsoft.EventGrid.SubscriptionValidationEvent response  -- >" + cloudEvent.Data);

            var responseData = new SubscriptionValidationResponse
            {
                ValidationResponse = eventData.ValidationCode
            };

            if (responseData.ValidationResponse != null)
            {
                return Results.Json(responseData,statusCode:200);
            }
        }

        if (cloudEvent.EventType == SystemEventNames.AcsRecordingFileStatusUpdated)
        {
            logger.LogInformation($"Event type is -- > {cloudEvent.EventType}");

            logger.LogInformation("Microsoft.Communication.RecordingFileStatusUpdated response  -- >" + cloudEvent.Data);

            var eventData = cloudEvent.Data.ToObjectFromJson<AcsRecordingFileStatusUpdatedEventData>();
            var _contentLocation = eventData.RecordingStorageInfo.RecordingChunks[0].ContentLocation;

            var callRecording = callAutomationClient.GetCallRecording();
            callRecording.DownloadTo(new Uri(_contentLocation), "wwwroot/audio/Recording_File.wav");
            logger.LogInformation("Start processing metadata -- >");

        }

        return Results.Json("Result : Success",statusCode:200);
    }
    catch (Exception ex)
    {
        return Results.Json(new { Exception = ex });
    }});



if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}




app.UseCors("AllowAll");
app.UseStaticFiles();
app.Run();
