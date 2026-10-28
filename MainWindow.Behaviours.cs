using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LBAAssembler.Maps;
using LBAAssembler.Scenes;

namespace LBAAssembler;

// The actor attributes windows' "Apply a behaviour…" (ActorBehaviours, BehaviourPickWindow): ready-made scripts for an actor already in its
// scene file -- the game's own actors of its kind, templates, or none -- written into the scene as one undo step (an open map's scene: into
// the map), the views read again. (The scene editors apply them in their own document.)
public partial class MainWindow
{
    private void ApplyBehaviourToLba2Actor(Window owner, int actorIndex)
    {
        if (nativeRenderer.RendererLibrary is not { } library || Lba2ActorPersistence.Locate(library, actorIndex) is not { } located)
        {
            MessageBox.Show(owner, "This actor's scene isn't the one loaded now, so its scripts can't be given here.", "Apply a behaviour", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        ApplyBehaviour(owner, SceneGame.Lba2, gameRoot, located.Scene, located.IndexInScene + 1);
    }

    private void ApplyBehaviourToLba1Actor(Window owner, int scene, int actor)
        => ApplyBehaviour(owner, SceneGame.Lba1, EditorSettings.Current.Lba1Directory, scene, actor);

    private void ApplyBehaviour(Window owner, SceneGame game, string directory, int scene, int index)
    {
        var session = game == SceneGame.Lba1 ? lba1Session : scriptSession;
        if (session.EditedScenes.Contains(scene))
        {
            MessageBox.Show(owner, $"Scene {scene} has unsaved script edits. Save or revert them first, so they and the behaviour don't overwrite each other.", "Apply a behaviour",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var store = new SceneStore(game, directory);
        SceneModel model;
        try { model = store.Load(scene); }
        catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or SceneEditException)
        {
            MessageBox.Show(owner, $"Couldn't read scene {scene}: {error.Message}", "Apply a behaviour", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (index <= 0 || index >= model.Actors.Count)
        {
            MessageBox.Show(owner, "This actor isn't in the scene file yet: Apply first to add it, then give it a behaviour.", "Apply a behaviour", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (game == SceneGame.Lba2 && index == 1 && SceneOps.IsMecaPenguinSlot(model.Actors[1]))
        {
            MessageBox.Show(owner, "Actor 1 is the slot the game keeps for the Meca-penguin: it hides whatever is there.", "Apply a behaviour", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (BehaviourPickWindow.ForActor(owner, store, model, index, $"scene {scene}") is not { } chosen) return;
        List<string> notes;
        try
        {
            notes = ActorBehaviours.Apply(store, model, index, chosen.Behaviour, chosen.Text);
            store.Save(scene, model, description: $"Give actor {index} of scene {scene} a behaviour ({chosen.Behaviour.Name.ToLowerInvariant()})");
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(owner, $"Not saved: {error.Message}", "Apply a behaviour", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        // (the views read the scene again; its actor windows close: what they hold is the scene as it was)
        ReloadScenesAfterWrite(game, new[] { scene });
        var where = MapWorkspace.For(directory) is { } map ? $"into the map \"{map.Name}\" (File > Save map writes its file)" : "to the game's files";
        SetStatus($"Actor {index} of scene {scene}: {chosen.Behaviour.Name} -- {chosen.Behaviour.Description}. Saved {where}." +
                  (notes.Count > 0 ? $" {notes.Count} change{(notes.Count > 1 ? "s" : "")} to fit the scene." : ""), StatusKind.Success);
        if (notes.Count > 0)
            MessageBox.Show(this, $"Actor {index} of scene {scene}: {chosen.Behaviour.Name}, saved {where}. To fit the scene:\n\n- " + string.Join("\n- ", notes.Take(14)) +
                (notes.Count > 14 ? $"\n... {notes.Count - 14} more" : ""), "Apply a behaviour", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
