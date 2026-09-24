#!/usr/bin/env python3
"""Mechanical part of the WPF -> Avalonia port of LBA Assembler.

Rewrites the WPF spellings that have a one-to-one Avalonia (or Compat/) equivalent in the editor's own UI sources and
XAML. Everything it cannot do mechanically (control templates, docking, the drawing code) is left for a manual pass;
this script only exists so that pass starts from a consistent base. Idempotent: running it twice changes nothing.
"""
import re, sys, os, pathlib

ROOT = pathlib.Path(__file__).resolve().parents[2]

CS_FILES = """ActorAttributesWindow.axaml.cs ActorScriptWindow.axaml.cs AssetEditorWindow.cs CommunityRendererBackend.cs
DummyBodyPreview.cs EmbeddedGameHost.cs FilterableComboBox.cs GridEditorWindow.cs IslandDocument.cs IslandEditorView.cs
Lba1/Lba1ActorImages.cs Lba1/Lba1GridRenderer.cs Lba1/Lba1SpriteLibrary.cs Lba1ActorAttributesWindow.axaml.cs
Lba1LoadoutWindow.cs Lba1PlayView.axaml.cs Lba1SceneEditorWindow.axaml.cs Lba2SceneEditorWindow.axaml.cs ListPickWindow.cs
MainWindow.Audio.cs MainWindow.Lba2Areas.cs MainWindow.Menus.cs MainWindow.Modes.cs MainWindow.Placement.cs
MainWindow.Play.cs MainWindow.Terrain.cs MainWindow.Zones.cs MainWindow.axaml.cs MinimapPopupWindow.axaml.cs
ObjectBrowserWindow.cs SettingsWindow.axaml.cs SoftwareTerrainRenderer.cs TopDownMapRenderer.cs UiBrushes.cs UiBusy.cs
WindowLifecycle.cs ZoneOverlay.cs MainWindow.BodyDebugPreview.cs""".split()

XAML_FILES = """MainWindow.axaml ActorAttributesWindow.axaml ActorScriptWindow.axaml Lba1ActorAttributesWindow.axaml
Lba1PlayView.axaml Lba1SceneEditorWindow.axaml Lba2SceneEditorWindow.axaml MinimapPopupWindow.axaml SettingsWindow.axaml""".split()

USINGS = [
    "using Avalonia;", "using Avalonia.Controls;", "using Avalonia.Controls.Primitives;", "using Avalonia.Input;",
    "using Avalonia.Interactivity;", "using Avalonia.Layout;", "using Avalonia.Media;", "using Avalonia.Media.Imaging;",
    "using Avalonia.Threading;",
]

# (WPF event, Avalonia event, button guard)
MOUSE_EVENTS = {
    "MouseLeftButtonDown": ("PointerPressed", "IsLeft"), "MouseLeftButtonUp": ("PointerReleased", "IsLeft"),
    "MouseRightButtonDown": ("PointerPressed", "IsRight"), "MouseRightButtonUp": ("PointerReleased", "IsRight"),
    "PreviewMouseLeftButtonDown": ("PointerPressed", "IsLeft"), "PreviewMouseLeftButtonUp": ("PointerReleased", "IsLeft"),
    "PreviewMouseRightButtonDown": ("PointerPressed", "IsRight"), "PreviewMouseRightButtonUp": ("PointerReleased", "IsRight"),
    "MouseDown": ("PointerPressed", None), "MouseUp": ("PointerReleased", None), "PreviewMouseDown": ("PointerPressed", None),
    "PreviewMouseUp": ("PointerReleased", None), "MouseMove": ("PointerMoved", None), "PreviewMouseMove": ("PointerMoved", None),
    "MouseWheel": ("PointerWheelChanged", None), "PreviewMouseWheel": ("PointerWheelChanged", None),
    "MouseLeave": ("PointerExited", None), "MouseEnter": ("PointerEntered", None), "MouseDoubleClick": ("DoubleTapped", None),
    "PreviewKeyDown": ("KeyDown", None), "PreviewKeyUp": ("KeyUp", None), "Checked": ("IsCheckedChanged", None),
    "Unchecked": ("IsCheckedChanged", None), "PreviewTextInput": ("TextInput", None),
}

guards = {}   # handler method name -> guard property (IsLeft / IsRight), collected from XAML and C# wiring


def cs_transform(text, had_shapes):
    # usings
    lines = text.split("\n")
    out = []
    inserted = False
    for line in lines:
        s = line.strip()
        if re.match(r"using (System\.Windows[\w.]*|Microsoft\.Win32|System\.Drawing[\w.]*|AvalonDock[\w.]*|System\.Media|System\.Configuration|System\.Data)\s*;", s) \
           or re.match(r"using Forms = System\.Windows\.Forms;", s):
            if not inserted:
                out.extend(USINGS)
                if had_shapes: out.append("using Avalonia.Controls.Shapes;")
                if "new Run(" in text: out.append("using Avalonia.Controls.Documents;")
                inserted = True
            continue
        out.append(line)
    text = "\n".join(out)
    if not inserted and "Avalonia" not in text:
        text = "\n".join(USINGS) + "\n" + text

    r = [
        (r"\bSystem\.Windows\.Threading\.DispatcherPriority\b", "DispatcherPriority"),
        (r"\bSystem\.Windows\.Threading\.Dispatcher\b", "Dispatcher"),
        (r"\bSystem\.Windows\.Controls\.Primitives\.TextBoxBase\b", "TextBox"),
        (r"\bSystem\.Windows\.Controls\.Primitives\.PlacementMode\b", "PlacementMode"),
        (r"\bSystem\.Windows\.Controls\.Primitives\.", ""),
        (r"\bSystem\.Windows\.Media\.Effects\.", ""),
        (r"\bSystem\.Windows\.Media\.Imaging\.", ""),
        (r"\bSystem\.Windows\.Media\.", ""),
        (r"\bSystem\.Windows\.Controls\.", ""),
        (r"\bSystem\.Windows\.Input\.", ""),
        (r"\bSystem\.Windows\.Shapes\.", "Avalonia.Controls.Shapes."),
        (r"\bSystem\.Windows\.Window\b", "Window"),
        (r"\bSystem\.Windows\.", ""),
        (r"\bSystem\.ComponentModel\.CancelEventArgs\b", "WindowClosingEventArgs"),
        (r"\bCancelEventArgs\b", "WindowClosingEventArgs"),
        (r"\bRoutedPropertyChangedEventArgs<double>", "RangeBaseValueChangedEventArgs"),
        (r"\bTextBoxBase\b", "TextBox"),
        (r"\bFrameworkElement\b", "Control"),
        (r"\bUIElement\b", "Control"),
        (r"\bMouseButtonEventArgs\b", "PointerEventArgs"),
        (r"\bMouseEventArgs\b", "PointerEventArgs"),
        (r"\bMouseWheelEventArgs\b", "PointerWheelEventArgs"),
        (r"\bBitmapSource\.Create\(", "BitmapFactory.Create("),
        (r"\bBitmapSource\b", "Bitmap"),
        (r"\bInt32Rect\.Empty\b", "new PixelRect()"),
        (r"\bInt32Rect\b", "PixelRect"),
        (r"\bModifierKeys\b", "KeyModifiers"),
        (r"\bVisibility\.Hidden\b", "Visibility.Collapsed"),
        (r"new CroppedBitmap\(", "BitmapFactory.Crop("),
        (r"new WriteableBitmap\(([^;]*?), 96, 96, PixelFormats\.\w+, null\)", r"BitmapFactory.Writeable(\1)"),
        (r"new MatrixTransform\(", "CompatTransforms.Matrix("),
        (r"RenderOptions\.SetBitmapScalingMode\(([^,]+), BitmapScalingMode\.NearestNeighbor\)", r"RenderOptions.SetBitmapInterpolationMode(\1, BitmapInterpolationMode.None)"),
        (r"RenderOptions\.SetBitmapScalingMode\(([^,]+), BitmapScalingMode\.(HighQuality|Fant)\)", r"RenderOptions.SetBitmapInterpolationMode(\1, BitmapInterpolationMode.HighQuality)"),
        (r"RenderOptions\.SetBitmapScalingMode\(([^,]+), BitmapScalingMode\.\w+\)", r"RenderOptions.SetBitmapInterpolationMode(\1, BitmapInterpolationMode.LowQuality)"),
        (r"new FontFamily\(\"Consolas\"\)", "UiFonts.Mono"),
        (r"new FontFamily\(\"Segoe UI\"\)", "UiFonts.Text"),
        (r"(?<![\w.])Dispatcher\.(BeginInvoke|Invoke|InvokeAsync|CheckAccess|VerifyAccess)\b", r"Dispatcher.UIThread.\1"),
        (r"\bDispatcher\.Yield\(", "DispatcherCompat.Yield("),
        (r"\(object sender,", "(object? sender,"),
        (r"(?<![.\w])Brush\b(?!\()", "IBrush"),
        (r"\bOwner = ", "OwnerWindow = "),
        (r"\.Show\(\);", ".ShowOwned();"),
        (r"\be\.Delta\b", "e.WheelDelta"),
        (r"\bSnapsToDevicePixels = \w+,?\s*", ""),
        (r"\bUseLayoutRounding = \w+,?\s*", ""),
    ]
    for pat, rep in r:
        text = re.sub(pat, rep, text)

    # mouse event wiring in code: X.MouseLeftButtonDown += ...
    def wire(m):
        ev, rest = m.group(1), m.group(2)
        av, guard = MOUSE_EVENTS[ev]
        rest2 = rest
        if guard:
            # lambda with a block body
            m2 = re.match(r"\s*\((\w+), (\w+)\) => \{", rest)
            if m2:
                e = m2.group(2)
                if e == "_":
                    rest2 = re.sub(r"\((\w+), _\) => \{", r"(\1, e) => { if (!e.%s) return;" % guard, rest, count=1)
                else:
                    rest2 = re.sub(r"\((\w+), (\w+)\) => \{", r"(\1, \2) => { if (!\2.%s) return;" % guard, rest, count=1)
            else:
                m3 = re.match(r"\s*\((\w+), (\w+)\) => (.*);\s*$", rest)
                if m3:
                    e = m3.group(2) if m3.group(2) != "_" else "e"
                    rest2 = " (%s, %s) => { if (!%s.%s) return; %s; }" % (m3.group(1), e, e, guard, m3.group(3)) + ";"
                else:
                    m4 = re.match(r"\s*(\w+);\s*$", rest)
                    if m4:
                        guards[m4.group(1)] = guard
        return ".%s +=%s" % (av, rest2)
    text = re.sub(r"\.(%s) \+=(.*)" % "|".join(MOUSE_EVENTS), wire, text)
    return text


def apply_guards(text):
    # insert `if (!e.IsLeft) return;` at the start of the named handlers
    for name, guard in guards.items():
        pat = re.compile(r"(void %s\(object\?? \w+, (?:PointerEventArgs|PointerPressedEventArgs|PointerReleasedEventArgs) (\w+)\)\s*(?:\r?\n\s*)?\{)" % re.escape(name))
        def ins(m):
            e = m.group(2)
            body_after = text[m.end():m.end() + 80]
            if ("!%s.%s" % (e, guard)) in body_after: return m.group(1)
            return m.group(1) + " if (!%s.%s) return;" % (e, guard)
        text = pat.sub(ins, text)
    return text


XAML_ATTR = [
    (r'xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"', 'xmlns="https://github.com/avaloniaui"'),
    (r'\s+xmlns:xcad="[^"]*"', ""),
    (r'\bVisibility="Collapsed"', 'IsVisible="False"'), (r'\bVisibility="Hidden"', 'IsVisible="False"'), (r'\bVisibility="Visible"', 'IsVisible="True"'),
    (r'\bToolTip="', 'ToolTip.Tip="'),
    (r'\bStyle="\{StaticResource (\w+)\}"', r'Classes="\1"'),
    (r'\bInputGestureText="', 'InputGesture="'),
    (r'\bFontFamily="Segoe UI"', 'FontFamily="{StaticResource EditorFont}"'),
    (r'\bFontFamily="Consolas"', 'FontFamily="{StaticResource EditorMonoFont}"'),
    (r'\s+SnapsToDevicePixels="\w+"', ""), (r'\s+UseLayoutRounding="\w+"', ""), (r'\s+TextOptions\.\w+="[^"]*"', ""),
    (r'\s+KeyboardNavigation\.\w+="[^"]*"', ""), (r'\s+IsTextSearchEnabled="\w+"', ""), (r'\s+StaysOpenOnEdit="\w+"', ""),
    (r'\s+FocusVisualStyle="[^"]*"', ""), (r'\s+AllowsTransparency="\w+"', ""), (r'\s+PopupAnimation="\w+"', ""), (r'\s+StaysOpen="\w+"', ""),
    (r'\s+OverridesDefaultStyle="\w+"', ""),
    (r'\bRenderOptions\.BitmapScalingMode="NearestNeighbor"', 'RenderOptions.BitmapInterpolationMode="None"'),
    (r'\bRenderOptions\.BitmapScalingMode="(HighQuality|Fant)"', 'RenderOptions.BitmapInterpolationMode="HighQuality"'),
    (r'\bRenderOptions\.BitmapScalingMode="\w+"', 'RenderOptions.BitmapInterpolationMode="LowQuality"'),
    (r'\bPanel\.ZIndex="', 'ZIndex="'),
    (r'\bResizeMode="NoResize"', 'CanResize="False"'), (r'\bResizeMode="CanMinimize"', 'CanResize="False"'), (r'\s+ResizeMode="CanResize\w*"', ""),
    (r'\bIsCheckable="True"', 'ToggleType="CheckBox"'),
    (r'\bSelectionMode="Extended"', 'SelectionMode="Multiple"'),
]


def xaml_transform(text):
    for pat, rep in XAML_ATTR:
        text = re.sub(pat, rep, text)
    # events
    def ev(m):
        name, handler = m.group(1), m.group(2)
        av, guard = MOUSE_EVENTS[name]
        if guard: guards[handler] = guard
        return '%s="%s"' % (av, handler)
    text = re.sub(r'\b(%s)="(\w+)"' % "|".join(MOUSE_EVENTS), ev, text)
    # TextBox scroll bar visibility is an attached property in Avalonia
    def textbox(m):
        s = m.group(0)
        s = re.sub(r'(?<![.\w])(Vertical|Horizontal)ScrollBarVisibility=', r'ScrollViewer.\1ScrollBarVisibility=', s)
        return s
    text = re.sub(r"<TextBox\b[^>]*>", textbox, text, flags=re.S)
    return text


def main():
    for f in XAML_FILES:
        p = ROOT / f
        t = p.read_text(encoding="utf-8-sig")
        p.write_text(xaml_transform(t), encoding="utf-8")
    texts = {}
    for f in CS_FILES:
        p = ROOT / f
        t = p.read_text(encoding="utf-8-sig")
        had_shapes = "using System.Windows.Shapes;" in t or "Avalonia.Controls.Shapes;" in t
        texts[f] = cs_transform(t, had_shapes)
    for f, t in texts.items():
        (ROOT / f).write_text(apply_guards(t), encoding="utf-8")
    print("guards:", guards)

if __name__ == "__main__":
    main()
