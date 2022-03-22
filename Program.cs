using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Speaker;

string region = "westus";
VoiceProfile voiceProfile;
UserData? userData = null;

var _configuration = ConfigService.GetAppConfiguration();

var config = SpeechConfig.FromSubscription(_configuration["SubscriptionKey"], region);
var inputConfig = AudioConfig.FromDefaultMicrophoneInput();
using var recognizer = new SpeechRecognizer(config);
using var synthesizer = new SpeechSynthesizer(config);

if(!StorageService.HasEnrolledUser())
{
    await synthesizer.SpeakTextAsync("Welcome friend! It seems that you haven't enrolled for voice verification before");
    await synthesizer.SpeakTextAsync("Well then, let's get you enrolled");
    await synthesizer.SpeakTextAsync("What's your name?");
    var username = await recognizer.RecognizeOnceAsync();

    if (!string.IsNullOrEmpty(username.Text))
    {
        userData = new UserData 
        {
            UserId = Guid.NewGuid().ToString("N"),
            Username = username.Text,
        };
        await VerificationEnroll(config, synthesizer, username.Text);
    }
}
else
{
    userData = await StorageService.Read();
    await synthesizer.SpeakTextAsync($"Welcome back {userData.Username}");
    await synthesizer.SpeakTextAsync("Time to verify your identity");
}

// verify the user
await SpeakerVerify(config, synthesizer);

async Task VerificationEnroll(SpeechConfig config, SpeechSynthesizer speechSynthesizer, string username)
{
    using (var client = new VoiceProfileClient(config))
    {
        voiceProfile = await client.CreateProfileAsync(VoiceProfileType.TextIndependentVerification, "en-us");
    
        using (var audioInput = AudioConfig.FromDefaultMicrophoneInput())
        {
            await speechSynthesizer.SpeakTextAsync($"Enrolling profile.");

            VoiceProfileEnrollmentResult? result = null;
            while (result is null || result.RemainingEnrollmentsSpeechLength > TimeSpan.Zero)
            {
                await speechSynthesizer.SpeakTextAsync("Please continue speaking to add more data to the voice sample.");
                result = await client.EnrollProfileAsync(voiceProfile, audioInput);
                var remainingTime = result?.RemainingEnrollmentsSpeechLength?.TotalSeconds;
                await speechSynthesizer.SpeakTextAsync($"You have {remainingTime} of enrollment audio time needed");
            }

            if (result.Reason == ResultReason.EnrolledVoiceProfile)
            {
                userData.IsEnrolled = true;
                userData.ProfileId = voiceProfile.Id;
                await StorageService.Save(userData);
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
    var profile = new VoiceProfile(userData?.ProfileId, VoiceProfileType.TextIndependentVerification);
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

    await synthesizer.SpeakTextAsync($"Congratulations {userData?.Username}, you have been authenticated with score {result.Score}");
}
