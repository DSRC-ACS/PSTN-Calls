# Application Setup Guide

This document provides the necessary steps to configure and run the application for initiating outbound calls.

## Configuration Steps

In the `Program.cs` file, update the following placeholders with your actual data:

- `acsConnectionString`: Your Azure Communication Services resource connection string.
- `acsPhoneNumber`: The phone number provided by ACS to start outbound calls.
- `targetPhoneNumber`: The phone number that will receive the calls.
- `callbackUriHost`: The base URL of your application (can be an ngrok URL or an App Services URL).
- `cognitiveServiceEndpoint`: The endpoint URL of your Cognitive Services.

## Running the Application

Once the above configurations are set, run the application. You can test the outbound call functionality by navigating to the API interface and using the "Try it out" feature.

## Issues

### Issue 1: Transcription Delay

**Problem Description:** Users experience a 3 to 4-second delay in obtaining the transcribed text after speaking. (program.cs) line number 133

### Issue 2: Call Recording Start Code

- The code block for starting call recording is currently commented out(program.cs - Line ). Activating this code disrupts the existing functionalities. 

