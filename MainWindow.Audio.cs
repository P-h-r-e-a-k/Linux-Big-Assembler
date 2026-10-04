using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace LBAAssembler;

// The sound balance for playing scenes, per game: mute, and music / speech / effects levels (the games' own balance is off, the
// music drowns the speech). Shown on the PLAY tab. LBA2's engine reads its volumes when it starts, and while it plays they reach it
// at once through its control socket (its snd_* settings); the LBA1 play view applies them at once. They are saved with the
// editor's settings.
public partial class MainWindow
{
    private bool audioSyncing;
    private bool audioDirty;

    private AudioLevels CurrentAudio => currentGame == GameKind.Lba1 ? EditorSettings.Current.Lba1Audio : EditorSettings.Current.Lba2Audio;

    private void SyncAudioControls()
    {
        audioSyncing = true;
        try
        {
            var audio = CurrentAudio;
            var name = currentGame == GameKind.Lba1 ? "LBA1" : "LBA2";
            AudioHeading.Text = $"Sound for {name}";
            AudioMuteCheck.IsChecked = audio.Mute;
            AudioMusicSlider.Value = audio.Music; AudioVoicesSlider.Value = audio.Voices; AudioEffectsSlider.Value = audio.Effects;
            ShowAudioValues(audio);
            AudioMusicSlider.IsEnabled = AudioVoicesSlider.IsEnabled = AudioEffectsSlider.IsEnabled = !audio.Mute;
            PlayEngineOptions.Visibility = currentGame == GameKind.Lba2 ? Visibility.Visible : Visibility.Collapsed;
            PlayIntroText.Text = currentGame == GameKind.Lba2
                ? "The Play scene button starts the scene that is open in the game (LBA2's own engine) inside this window. It plays what is saved on disk."
                : "The Play scene button plays the scene that is open in the LBA1 play view inside this window. It plays what is saved on disk.";
            AudioNote.Text = currentGame == GameKind.Lba2
                ? "These change the sound at once while the game plays. A game started muted has no sound at all until it is restarted."
                : "These change the sound at once while a scene is playing.";
        }
        finally { audioSyncing = false; }
    }

    private void ShowAudioValues(AudioLevels audio)
    {
        AudioMusicValue.Text = audio.Music + "%";
        AudioVoicesValue.Text = audio.Voices + "%";
        AudioEffectsValue.Text = audio.Effects + "%";
    }

    private void AudioMute_Click(object sender, RoutedEventArgs e)
    {
        var audio = CurrentAudio;
        audio.Mute = AudioMuteCheck.IsChecked == true;
        AudioMusicSlider.IsEnabled = AudioVoicesSlider.IsEnabled = AudioEffectsSlider.IsEnabled = !audio.Mute;
        audioDirty = true;
        lba1Play?.ApplyAudio();
        PushLba2Audio();
        if (!audio.Mute && currentGame == GameKind.Lba2 && playing && playingGame == GameKind.Lba2 && Lba2Play.LastOptions is { Sound: false })
            AudioNote.Text = "The game was started muted, without sound: restart it (Restart) to hear it.";
    }

    private void AudioSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (audioSyncing) return;
        var audio = CurrentAudio;
        audio.Music = (int)AudioMusicSlider.Value; audio.Voices = (int)AudioVoicesSlider.Value; audio.Effects = (int)AudioEffectsSlider.Value;
        ShowAudioValues(audio);
        audioDirty = true;
        PushLba2Audio();
    }

    private void AudioReset_Click(object sender, RoutedEventArgs e)
    {
        var audio = CurrentAudio;
        var defaults = new AudioLevels();
        audio.Mute = false; audio.Music = defaults.Music; audio.Voices = defaults.Voices; audio.Effects = defaults.Effects;
        audioDirty = true;
        SyncAudioControls();
        lba1Play?.ApplyAudio();
        PushLba2Audio();
    }

    // LBA2 while it plays: the balance goes to the running game through its control socket, as the engine's own settings (0-127; the
    // CD volume is the music's too, and mute is the master volume). One command at a time: a slider being dragged sends only its
    // latest value once the one before has been answered.
    private bool lba2AudioSending, lba2AudioAgain;

    private async void PushLba2Audio()
    {
        if (!playing || playingGame != GameKind.Lba2 || currentGame != GameKind.Lba2) return;
        if (lba2AudioSending) { lba2AudioAgain = true; return; }
        lba2AudioSending = true;
        try
        {
            do
            {
                lba2AudioAgain = false;
                if (lba2Control is not { } client) return;
                var a = EditorSettings.Current.Lba2Audio;
                var music = AudioLevels.ToEngine(a.Music);
                var answer = await client.SendAsync($"snd_wave {AudioLevels.ToEngine(a.Effects)};snd_voice {AudioLevels.ToEngine(a.Voices)};snd_music {music};snd_cd {music};snd_master {(a.Mute ? 0 : 127)}");
                DebugLog.Log($"MainWindow: sound balance sent to the game: {answer.Replace('\n', ' ').Trim()}");
            } while (lba2AudioAgain);
        }
        catch (IOException error) { DebugLog.Log($"MainWindow: the sound balance didn't reach the game: {error.Message}"); }
        finally { lba2AudioSending = false; }
    }

    private void SaveAudioIfDirty()
    {
        if (!audioDirty) return;
        audioDirty = false;
        try { EditorSettings.Current.Save(); }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException) { DebugLog.Log($"MainWindow: saving the sound balance failed: {error.Message}"); }
    }
}
