using Azure.Communication;
using Azure.Communication.CallAutomation;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using static System.Net.Mime.MediaTypeNames;

namespace CallAutomation_OutboundCalling
{

    public class CallProcess
    {
        public static CommunicationIdentifier participant_id;
        
        // Define constants for the text checks to avoid typos and make changes easier
        public static async Task PlayPrompt(CallAutomationClient callAutomationClient, CallAutomationEventBase parsedEvent, string TexttoPlay)
        {
            try
            {
                var playSource = new TextSource(TexttoPlay)
                {
                    VoiceName= "en-US-ElizabethNeural"
                };


                var playResponse = await callAutomationClient.GetCallConnection(parsedEvent.CallConnectionId)
                    .GetCallMedia()
                    .PlayToAllAsync(playSource);
            }
            catch (Exception ex)
            {
            }

        }
        public static async Task RecordResponse(CallAutomationClient callAutomationClient, CallAutomationEventBase parsedEvent, string TexttoPlay)
        {
            var playSource = new TextSource(TexttoPlay)
            {
                VoiceName = "en-US-ElizabethNeural"
            };
            var recognizeOptions = new CallMediaRecognizeSpeechOptions(participant_id)
            {
                Prompt = playSource,
                //EndSilenceTimeout = TimeSpan.FromMilliseconds(1000),
                InitialSilenceTimeout = TimeSpan.FromSeconds(10),
                //InterruptPrompt = false,
                OperationContext = "OpenQuestionSpeechOrDtmf",

            };
            var recognizeResult = await callAutomationClient.GetCallConnection(parsedEvent.CallConnectionId)
                .GetCallMedia()
                .StartRecognizingAsync(recognizeOptions);

        }

        
        public static async Task EndCall(CallAutomationClient callAutomationClient, CallAutomationEventBase parsedEvent)
        {
            CallConnection callConnection = callAutomationClient.GetCallConnection(parsedEvent.CallConnectionId);
            await callConnection.HangUpAsync(true);
        }


        public static async Task<string> StartRecordingAsync(CallAutomationClient callAutomationClient, string serverCallId)
        {
            try
            {
                //CallAutomationClient callAutomationClient = new CallAutomationClient(Environment.GetEnvironmentVariable("ACS_CONNECTION_STRING"));
                StartRecordingOptions recordingOptions = new StartRecordingOptions(new ServerCallLocator(serverCallId));
                recordingOptions.RecordingChannel = RecordingChannel.Mixed;
                recordingOptions.RecordingContent = RecordingContent.Audio;
                recordingOptions.RecordingFormat = RecordingFormat.Wav;
                var startRecordingResponse = await callAutomationClient.GetCallRecording().StartAsync(recordingOptions).ConfigureAwait(false);
                return ($"RecordingId: {startRecordingResponse}");
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

    }
}
