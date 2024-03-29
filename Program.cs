﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Speaker;
using Microsoft.Identity.Client;

string subscriptionKey = "move key to KeyVault";
string region = "westus";
VoiceProfile voiceProfile;
string[] possibleAnswers = { "no", "i don't know", "don't know", "i do not know", "i can't remember", "i don't remember", "don't remember", "maybe", "i'm unsure", "no idea", "i'm not sure", "i am unsure" };

    var userId = await SignInAndGetUserId();

    var config = SpeechConfig.FromSubscription(subscriptionKey, region);
    var inputConfig = AudioConfig.FromDefaultMicrophoneInput();
    using var recognizer = new SpeechRecognizer(config);
    using var synthesizer = new SpeechSynthesizer(config);

    await synthesizer.SpeakTextAsync("Welcome friend! Have you enrolled for voice verification before?");

    var result = await recognizer.RecognizeOnceAsync();
    if (ResponseIsNegative(result.Text))
    {
        await synthesizer.SpeakTextAsync("Well then, let's get you enrolled");
        await synthesizer.SpeakTextAsync("What's your name?");
        var username = await recognizer.RecognizeOnceAsync();
        if (!string.IsNullOrEmpty(username.Text))
        {
            await VerificationEnroll(config, synthesizer, username.Text);
        }
    }

    await SpeakerVerify(config, synthesizer);

    bool ResponseIsNegative(string response)
    {
        if(string.IsNullOrEmpty(response))
            return true;

        if(response.Contains('.') || response.Contains('?') || response.Contains('!'))
        {
            var sanitizedInput = response.Remove(response.IndexOf('.'), 1).ToLower();
            return possibleAnswers.Contains(sanitizedInput);
        }
        
        return possibleAnswers.Contains(response.ToLower());
    }

    async Task VerificationEnroll(SpeechConfig config, SpeechSynthesizer speechSynthesizer, string username)
    {
        using (var client = new VoiceProfileClient(config))
        {
            voiceProfile = await client.CreateProfileAsync(VoiceProfileType.TextIndependentVerification, "en-us");
        
            using (var audioInput = AudioConfig.FromDefaultMicrophoneInput())
            {
                await speechSynthesizer.SpeakTextAsync($"Enrolling profile.");

                VoiceProfileEnrollmentResult result = null;
                while (result is null || result.RemainingEnrollmentsSpeechLength > TimeSpan.Zero)
                {
                    await speechSynthesizer.SpeakTextAsync("Please continue speaking to add more data to the voice sample.");
                    result = await client.EnrollProfileAsync(voiceProfile, audioInput);
                    var remainingTime = result.RemainingEnrollmentsSpeechLength.Value.TotalSeconds;
                    await speechSynthesizer.SpeakTextAsync($"You have {remainingTime} of enrollment audio time needed");
                }

                if (result.Reason == ResultReason.EnrolledVoiceProfile)
                {
                    await speechSynthesizer.SpeakTextAsync("You have successfully enrolled for voice authentication!");
                }
                else if (result.Reason == ResultReason.Canceled)
                {
                    var cancellation = VoiceProfileEnrollmentCancellationDetails.FromResult(result);
                    await speechSynthesizer.SpeakTextAsync($"Enrollment for {username} was cancelled: ErrorCode={cancellation.ErrorCode} ErrorDetails={cancellation.ErrorDetails}");
                }
            }
        }
    }

    async Task SpeakerVerify(SpeechConfig config, SpeechSynthesizer synthesizer)
    {
        var profile = new VoiceProfile("1f808d93-fcf5-4113-a5a5-1e441cac7dd5",VoiceProfileType.TextIndependentVerification);
        var speakerRecognizer = new SpeakerRecognizer(config, AudioConfig.FromDefaultMicrophoneInput());
        var model = SpeakerVerificationModel.FromProfile(profile);

        await synthesizer.SpeakTextAsync("Speak the passphrase to verify: 'My voice is my passport, please verify me.'");

        var result = await speakerRecognizer.RecognizeOnceAsync(model);
        if (result.Reason == ResultReason.Canceled)
        {
            await synthesizer.SpeakTextAsync("Voice verification cancelled");
            return;
        }

        //{profileMapping[result.ProfileId]}
        await synthesizer.SpeakTextAsync($"Congratulations, you have been authenticated with score {result.Score}");
    }

    async Task<string> SignInAndGetUserId()
    {
        var clientId = "a79ccd89-0bb4-4909-92fa-737e246e3bec";
        var tenantId = "b55f0c51-61a7-45c3-84df-33569b247796";
        
        IPublicClientApplication app = PublicClientApplicationBuilder
            .Create(clientId)
            .WithTenantId(tenantId)
            .Build();
        
        AuthenticationResult result = null;
        try
        {
            result = await app.AcquireTokenWithDeviceCode(new string[] {"user.read"}, 
                deviceCodeCallback =>
                {
                    Console.WriteLine(deviceCodeCallback.Message);
                    return Task.FromResult(0);
                }).ExecuteAsync().ConfigureAwait(false);
        }
        catch(MsalServiceException ex)
        {
            Console.WriteLine($"MSAL error: {ex.Message}");
        }

        var handler = new JwtSecurityTokenHandler();
        var idToken = handler.ReadJwtToken(result.IdToken);
        return idToken.Claims.First(x => x.Type.Equals("oid")).Value;
    }
