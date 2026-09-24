// The WPF -> Avalonia port keeps the editor's own code as close to the original as it can: the pieces of WPF that
// Avalonia spells differently (Visibility, MessageBox, the file dialogs, Keyboard/Mouse, the bitmap factories ...) are
// provided once, in this folder, as small shims and C# 14 extension members in the LBAAssembler namespace itself (so
// they win over Avalonia's own, obsolete, OpenFileDialog and friends), and the windows read the same as before.
