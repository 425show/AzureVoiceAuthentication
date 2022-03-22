using System;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Speaker;
using Microsoft.Identity.Client;
using System.Linq;

string subscriptionKey = "8c8003be1b8240c0a444e93928a0de62";
string region = "westus";
VoiceProfile voiceProfile;
UserData userData;
string[] possibleAnswers = { "no", "i don't know", "don't know", "i do not know", "i can't remember", "i don't remember", "don't remember", "maybe", "i'm unsure", "no idea", "i'm not sure", "i am unsure" };
(string,string) userDetails;

if(!StorageService.HasEnrolledUser())
{
    Console.WriteLine("You need to authenticate first");
    userDetails = await AuthenticationService.SignInAndGetUserId();
}
else
{
    userData = await StorageService.Read();
    userDetails = (userData.UserId, userData.Username);
}

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
        await VerificationEnroll(config, synthesizer);
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
                userData.IsEnrolled = true;
                userData.ProfileId = voiceProfile.Id;
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
        await synthesizer.SpeakTextAsync("Voice authentication cancelled");
        return;
    }
    if (result.Score < 0.7)
    {
        await synthesizer.SpeakTextAsync($"Voice authentication was unsuccessful. Please try again!");
        return;
    }
    //{profileMapping[result.ProfileId]}
    await synthesizer.SpeakTextAsync($"Congratulations, you have been authenticated with score {result.Score}");
}
