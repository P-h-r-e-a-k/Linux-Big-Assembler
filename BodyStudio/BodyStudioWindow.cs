using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using LBAAssembler;
// the app's WPF-style blocking dialog shims (Compat/FileDialogs.cs), not Avalonia's obsolete ones of the same names
using OpenFileDialog=LBAAssembler.OpenFileDialog;
using OpenFolderDialog=LBAAssembler.OpenFolderDialog;
using SaveFileDialog=LBAAssembler.SaveFileDialog;

namespace LbaBodyStudio;

// Body Studio's window (an Avalonia Window built in code, formerly a WinForms Form): a settings column on the left, the 3D preview
// and the reference picture in tabs on the right, a status line along the bottom.
public sealed class BodyStudioWindow : Window
{
    readonly TextBox imagePath=new(),game1=new(),game2=new(),output=new();
    readonly TextBox bandanaText=new(){MaxLength=8};
    readonly CheckBox headDetails=Check("Add bandana, teeth and rear ties");
    readonly ComboBox target=Combo("Both","LBA1","LBA2"),layout=Combo("Front + back","Single front"),mask=Combo("Dark subject","Light subject","Transparent background","Background colour");
    readonly ComboBox method=Combo("New humanoid","Fit template");
    readonly NumericUpDown body1=Number(0,10000,0),body2=Number(0,10000,0),threshold=Number(1,254,45),fit=Number(0,100,80),height=Number(25,300,100),width=Number(25,300,100),depth=Number(25,300,100),head=Number(25,150,70),budget=Number(0,540,460);
    readonly CheckBox autoCrop=Check("Auto crop subject",true),flip=Check("Mirror image projection"),negative=Check("Front faces negative Z") /* actual default comes from Settings.NegativeZFront via Apply() below */,archive=Check("Include a separate BODY.HQR copy",true);
    readonly NumericUpDown flatColours=Number(2,40,14);
    readonly CheckBox pairImage=Check("The picture holds a front view (left) and a back view (right)"),symmetric=Check("Make the figure symmetric (copy the left half)"),lit=Check("Game lighting: shade the body like the game's own characters",true);
    readonly Dictionary<int,BodyStyleStats> styleStats=[];
    readonly ModelView preview=new();
    // A dark neutral backdrop, not the 3D view's light-blue one: a loaded picture is usually a silhouette on a transparent background,
    // and a light backdrop shows through the transparent parts and the antialiased edges, washing out what should read as black.
    readonly Image reference=new(){Stretch=Stretch.Uniform};
    Avalonia.Media.Imaging.Bitmap? referenceBitmap;
    readonly TextBlock status=new(){Text="Choose an image, generate, then inspect the preview before exporting.",TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center,Foreground=Brush(Renderer.Text)};
    readonly Button generate=new(){Content="Generate preview"},export=new(){Content="Export selected games",IsEnabled=false};
    readonly ComboBox previewGame=Combo("LBA1","LBA2");
    readonly List<Generated> generated=[];
    Settings? generatedSettings;
    public BodyStudioWindow()
    {
        Title="LBA Assembler — Body Studio";Width=1400;Height=930;MinWidth=1050;MinHeight=720;FontSize=13;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=Brushes.White;
        var root=new DockPanel();Content=root;
        var statusBar=new Border(){Height=52,Padding=new Thickness(16,8,16,8),Background=Brush(Renderer.PanelBackground),Child=status};DockPanel.SetDock(statusBar,Dock.Bottom);root.Children.Add(statusBar);
        // the settings column (fixed 390 wide, at least 360) | splitter | the preview
        var main=new Grid(){Background=Brush(Renderer.Border)};root.Children.Add(main);
        main.ColumnDefinitions.Add(new ColumnDefinition(390,GridUnitType.Pixel){MinWidth=360});main.ColumnDefinitions.Add(new ColumnDefinition(4,GridUnitType.Pixel));main.ColumnDefinitions.Add(new ColumnDefinition(1,GridUnitType.Star));
        var splitter=new GridSplitter(){Background=Brush(Renderer.Border)};Grid.SetColumn(splitter,1);main.Children.Add(splitter);
        var left=new DockPanel(){Background=Brush(Renderer.PanelBackground)};Grid.SetColumn(left,0);main.Children.Add(left);
        var fields=new StackPanel(){Margin=new Thickness(18)};
        void Add(Control c){c.Margin=new Thickness(0,0,0,10);fields.Children.Add(c);}
        void Label(string text){Add(new TextBlock(){Text=text,FontWeight=FontWeight.Bold});}
        void Field(string text,Control c){Add(new TextBlock(){Text=text,Margin=new Thickness(0,2,0,3)});Add(c);}
        // Description text (wrapped to the column) reads softer than headings and field names.
        void Note(string text){Add(new TextBlock(){Text=text,TextWrapping=TextWrapping.Wrap,MaxWidth=325,HorizontalAlignment=HorizontalAlignment.Left,Foreground=Brush(Renderer.TextMuted)});}
        Control Browse(TextBox text,bool file)
        {
            var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition(1,GridUnitType.Star));row.ColumnDefinitions.Add(new ColumnDefinition(42,GridUnitType.Pixel));row.Children.Add(text);
            var button=new Button(){Content="…",HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Center,Margin=new Thickness(4,0,0,0)};Grid.SetColumn(button,1);row.Children.Add(button);
            button.Click+=(_,_)=>{if(file){var d=new OpenFileDialog(){Filter="Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif"};if(d.ShowDialog(this)==true){text.Text=d.FileName;LoadImage();}}else{var d=new OpenFolderDialog(){InitialDirectory=text.Text};if(d.ShowDialog(this)==true)text.Text=d.FolderName;}};
            return row;
        }
        Label("LBA BODY STUDIO");Note("Fit a native character template to a reference image. Both games export independently with their own rig and palette.");
        Field("Reference image",Browse(imagePath,true));Field("Image layout",layout);Field("Subject mask",mask);Field("Mask threshold",threshold);Add(autoCrop);Add(flip);Add(negative);
        Label("Flat picture for the game");Note("Turn any picture into the flat, few-colour, palette-exact picture the generator works best with (the game's own bodies use a dozen or so colours). Or export a game body as such a picture to see what one looks like: the template's game and index are chosen below.");
        Field("Flat colours",flatColours);Add(pairImage);Add(symmetric);
        var convertButton=new Button(){Content="Convert the reference image to game style"};Add(convertButton);convertButton.Click+=async(_,_)=>await ConvertStyle();
        var sheetButton=new Button(){Content="Export a flat sheet of the selected template…"};Add(sheetButton);sheetButton.Click+=(_,_)=>ExportSheet();
        Add(lit);
        Label("Head details — optional");Add(headDetails);Field("Bandana lettering (up to 8 characters)",bandanaText);
        Note("New humanoid mode only. Builds actual head-bone geometry. Uses a compact block font; text is supplied here, not read automatically from the image.");
        headDetails.IsCheckedChanged+=(_,_)=>bandanaText.IsEnabled=headDetails.IsChecked==true;
        Label("Game assets and template");Field("LBA1 installation",Browse(game1,false));Field("LBA1 body index (zero-based)",body1);Field("LBA2 installation",Browse(game2,false));Field("LBA2 body index (zero-based)",body2);
        var originalButton=new Button(){Content="Preview original selected template"};Add(originalButton);originalButton.Click+=(_,_)=>PreviewOriginal();
        Label("Shape and detail");Field("Mesh generation",method);Field("Silhouette fit (%) — template mode",fit);Field("Height (%)",height);Field("Width (%)",width);Field("Depth (%) — inferred",depth);Field("Head proportion (%) — 70 recommended",head);Field("Refinement primitive budget (0 = no extra detail)",budget);
        Label("Export");Field("Output format",target);Field("Output folder",Browse(output,false));Add(archive);
        var buttons=new StackPanel(){Orientation=Orientation.Horizontal,Spacing=6,Height=50,Margin=new Thickness(12,8,0,0)};buttons.Children.Add(generate);buttons.Children.Add(export);DockPanel.SetDock(buttons,Dock.Bottom);left.Children.Add(buttons);
        var projectButtons=new StackPanel(){Orientation=Orientation.Horizontal,Spacing=6};var save=new Button(){Content="Save settings"};var load=new Button(){Content="Load settings"};projectButtons.Children.Add(save);projectButtons.Children.Add(load);Add(projectButtons);
        Note("Best input: upright full-body views on a plain background. Optional head details replace projected facial colours with stylised geometry and reserve the mesh budget for the face. Check Head close-up, then the whole body. In-game appearance still needs testing.");
        left.Children.Add(new ScrollViewer(){Content=fields,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        var right=new Grid();right.RowDefinitions.Add(new RowDefinition(48,GridUnitType.Pixel));right.RowDefinitions.Add(new RowDefinition(1,GridUnitType.Star));Grid.SetColumn(right,2);main.Children.Add(right);
        var toolbar=new StackPanel(){Orientation=Orientation.Horizontal,Spacing=8,Margin=new Thickness(8),VerticalAlignment=VerticalAlignment.Center};previewGame.Width=95;toolbar.Children.Add(previewGame);
        var wire=Check("Wireframe");var bones=Check("Bones");var front=new Button(){Content="Front"};var back=new Button(){Content="Back"};toolbar.Children.Add(wire);toolbar.Children.Add(bones);toolbar.Children.Add(front);toolbar.Children.Add(back);right.Children.Add(toolbar);
        var closeUp=Check("Head close-up");toolbar.Children.Add(closeUp);closeUp.IsCheckedChanged+=(_,_)=>{preview.HeadOnly=closeUp.IsChecked==true;preview.Invalidate();};
        // The tabs take the app's own theme (Theme.axaml) like every other window's; nothing is owner-drawn any more.
        var tabs=new TabControl(){Padding=new Thickness(0)};Grid.SetRow(tabs,1);
        var modelTab=new TabItem(){Header="3D body",Content=preview};var imageTab=new TabItem(){Header="Reference image",Content=new Border(){Background=Brush(Renderer.KeyBackground),Child=reference}};tabs.Items.Add(modelTab);tabs.Items.Add(imageTab);right.Children.Add(tabs);
        wire.IsCheckedChanged+=(_,_)=>{preview.Wire=wire.IsChecked==true;preview.Invalidate();};bones.IsCheckedChanged+=(_,_)=>{preview.Bones=bones.IsChecked==true;preview.Invalidate();};front.Click+=(_,_)=>{preview.Yaw=negative.IsChecked==true?0:MathF.PI;preview.Invalidate();};back.Click+=(_,_)=>{preview.Yaw=negative.IsChecked==true?MathF.PI:0;preview.Invalidate();};previewGame.SelectionChanged+=(_,_)=>ShowGenerated();
        generate.Click+=async(_,_)=>await Generate();export.Click+=async(_,_)=>await Export();
        save.Click+=(_,_)=>{var d=new SaveFileDialog(){Filter="Body Studio project|*.json",FileName="body-project.json"};if(d.ShowDialog(this)==true)Try(()=>File.WriteAllText(d.FileName,JsonSerializer.Serialize(ReadSettings(),new JsonSerializerOptions{WriteIndented=true})));};
        load.Click+=(_,_)=>{var d=new OpenFileDialog(){Filter="Body Studio project|*.json"};if(d.ShowDialog(this)==true)Try(()=>{Apply(JsonSerializer.Deserialize<Settings>(File.ReadAllText(d.FileName))??throw new InvalidDataException("Empty project."));LoadImage();});};
        Apply(new Settings(){OutputFolder=Path.Combine(AppContext.BaseDirectory,"Body Exports"),Lba1Folder=LBAAssembler.EditorSettings.Current.Lba1Directory,Lba2Folder=LBAAssembler.EditorSettings.Current.GameDirectory});
        // Any setting change invalidates the export snapshot until regenerated.
        foreach(Control c in Descendants(fields))
        {
            if(c is TextBox t)t.TextChanged+=(_,_)=>InvalidateGeneration();
            if(c is NumericUpDown n)n.ValueChanged+=(_,_)=>InvalidateGeneration();
            if(c is ComboBox cb)cb.SelectionChanged+=(_,_)=>InvalidateGeneration();
            if(c is CheckBox ch)ch.IsCheckedChanged+=(_,_)=>InvalidateGeneration();
        }
        ApplyTheme();
        Closed+=(_,_)=>{referenceBitmap?.Dispose();referenceBitmap=null;};
    }
    // Matches the rest of the app's own light theme (Theme.axaml) instead of plain control defaults. Runs once, over every
    // control the window ends up with, rather than colouring each one where it's built.
    void ApplyTheme()
    {
        foreach(var c in Descendants(this))
        {
            switch(c)
            {
                case TextBox tb: tb.Background=Brush(Renderer.FieldBackground);tb.Foreground=Brush(Renderer.Text);tb.BorderBrush=Brush(Renderer.Border);tb.BorderThickness=new Thickness(1);break;
                case NumericUpDown nu: nu.Background=Brush(Renderer.FieldBackground);nu.Foreground=Brush(Renderer.Text);nu.BorderBrush=Brush(Renderer.Border);nu.BorderThickness=new Thickness(1);break;
                case ComboBox combo: combo.Background=Brush(Renderer.FieldBackground);combo.Foreground=Brush(Renderer.Text);combo.BorderBrush=Brush(Renderer.Border);break;
                case Button b when b!=generate&&b!=export:
                    b.Background=Brush(Renderer.ButtonBackground);b.Foreground=Brush(Renderer.Text);b.BorderBrush=Brush(Renderer.ButtonBorder);b.BorderThickness=new Thickness(1);
                    break;
                case CheckBox chk: chk.Foreground=Brush(Renderer.Text);break;
                // Description text (the only text blocks with a wrap width) reads softer than headings and field names.
                case TextBlock lbl when lbl!=status: lbl.Foreground=lbl.MaxWidth<double.PositiveInfinity?Brush(Renderer.TextMuted):Brush(Renderer.Text);break;
            }
        }
        foreach(var b in new[]{generate,export})
        {
            b.Classes.Add("accent");b.Background=Brush(Renderer.Accent);b.Foreground=Brushes.White;b.FontWeight=FontWeight.Bold;b.BorderBrush=Brush(Renderer.Accent);
        }
        // the buttons' hover colours (WinForms' FlatAppearance.MouseOverBackColor): the theme's template paints the presenter's background
        Styles.Add(HoverStyle(x=>x.OfType<Button>().Not(y=>y.Class("accent")),Renderer.ButtonHover));
        Styles.Add(HoverStyle(x=>x.OfType<Button>().Class("accent"),Argb.Lighten(Renderer.Accent,0.25f)));
    }
    internal static Style HoverStyle(Func<Selector?,Selector> button,uint colour)
    {
        var style=new Style(x=>button(x).Class(":pointerover").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"));
        style.Setters.Add(new Setter(ContentPresenter.BackgroundProperty,Brush(colour)));
        return style;
    }
    // Every control under `c` (the panels' children, a decorator's child, a content control's content, a tab control's items).
    internal static IEnumerable<Control> Descendants(Control c)
    {
        IEnumerable<Control> children=c switch
        {
            Panel p=>p.Children,
            Decorator d=>d.Child is Control child?[child]:[],
            ItemsControl items=>items.Items.OfType<Control>(),
            ContentControl content=>content.Content is Control inner?[inner]:[],
            _=>[],
        };
        foreach(var child in children){yield return child;foreach(var d in Descendants(child))yield return d;}
    }
    internal static IBrush Brush(uint argb)=>new SolidColorBrush(Color.FromUInt32(argb));
    void InvalidateGeneration(){generatedSettings=null;export.IsEnabled=false;}
    static ComboBox Combo(params string[] items){var c=new ComboBox(){HorizontalAlignment=HorizontalAlignment.Stretch};foreach(var item in items)c.Items.Add(item);c.SelectedIndex=0;return c;}
    static NumericUpDown Number(int min,int max,int value)=>new(){Minimum=min,Maximum=max,Value=value,Increment=1,FormatString="0"};
    static CheckBox Check(string text,bool isChecked=false)=>new(){Content=text,IsChecked=isChecked};
    static string Text(ComboBox c)=>c.SelectedItem as string??"";
    static int Value(NumericUpDown n)=>(int)(n.Value??0);
    Settings ReadSettings()=>new(){ImagePath=imagePath.Text??"",Lba1Folder=game1.Text??"",Lba2Folder=game2.Text??"",Lba1Body=Value(body1),Lba2Body=Value(body2),Target=Text(target),Layout=Text(layout),Method=Text(method),Mask=Text(mask),Threshold=Value(threshold),AutoCrop=autoCrop.IsChecked==true,FlipFront=flip.IsChecked==true,NegativeZFront=negative.IsChecked==true,Fit=Value(fit)/100f,Height=Value(height)/100f,Width=Value(width)/100f,Depth=Value(depth)/100f,HeadScale=Value(head)/100f,DetailBudget=Value(budget),HeadDetails=headDetails.IsChecked==true,BandanaText=bandanaText.Text??"",ArchiveCopy=archive.IsChecked==true,OutputFolder=output.Text??"",Lit=lit.IsChecked==true};
    void Apply(Settings s)
    {
        imagePath.Text=s.ImagePath;game1.Text=s.Lba1Folder;game2.Text=s.Lba2Folder;output.Text=s.OutputFolder;
        headDetails.IsChecked=s.HeadDetails;bandanaText.Text=s.BandanaText;bandanaText.IsEnabled=s.HeadDetails;
        body1.Value=Math.Clamp(s.Lba1Body,0,10000);body2.Value=Math.Clamp(s.Lba2Body,0,10000);threshold.Value=Math.Clamp(s.Threshold,1,254);fit.Value=Math.Clamp((decimal)s.Fit*100,0,100);height.Value=Math.Clamp((decimal)s.Height*100,25,300);width.Value=Math.Clamp((decimal)s.Width*100,25,300);depth.Value=Math.Clamp((decimal)s.Depth*100,25,300);head.Value=Math.Clamp((decimal)s.HeadScale*100,25,150);budget.Value=Math.Clamp(s.DetailBudget,0,540);
        lit.IsChecked=s.Lit;target.SelectedItem=s.Target;layout.SelectedItem=s.Layout;method.SelectedItem=s.Method;mask.SelectedItem=s.Mask;autoCrop.IsChecked=s.AutoCrop;flip.IsChecked=s.FlipFront;negative.IsChecked=s.NegativeZFront;archive.IsChecked=s.ArchiveCopy;
    }
    void LoadImage()=>Try(()=>{var image=FlatBitmap.Load(imagePath.Text??"");var bitmap=BitmapFactory.FromBgra(image.Width,image.Height,image.Bgra);reference.Source=bitmap;referenceBitmap?.Dispose();referenceBitmap=bitmap;});
    void ShowGenerated(){preview.Model=generated.FirstOrDefault(g=>g.Body.Game==previewGame.SelectedIndex+1);preview.Invalidate();}
    void PreviewOriginal()=>Try(()=>{int game=previewGame.SelectedIndex+1;var s=ReadSettings();string folder=game==1?s.Lba1Folder:s.Lba2Folder;int index=game==1?s.Lba1Body:s.Lba2Body;string path=Generator.BodyArchive(folder);preview.Model=new(Body.Read(new Hqr(path).Read(index),game),Generator.Palette(folder),index,path,"");preview.Invalidate();status.Text="Template shown from "+Path.GetFileName(path)+". Generate to return to your image-derived body.";});
    async Task Generate()
    {
        var s=ReadSettings();IsEnabled=false;status.Text="Fitting geometry, projecting colours, and checking native bodies…";
        try
        {
            var models=await Task.Run(()=>{var list=new List<Generated>();if(s.Target is "Both" or "LBA1")list.Add(Generator.Generate(s,1));if(s.Target is "Both" or "LBA2")list.Add(Generator.Generate(s,2));return list;});
            generated.Clear();generated.AddRange(models);generatedSettings=s;previewGame.SelectedIndex=models[0].Body.Game-1;ShowGenerated();export.IsEnabled=true;LoadImage();status.Text="Preview ready. Drag to rotate; check front, back and bones. Export uses this exact generated snapshot.";
        }
        catch(Exception e){generatedSettings=null;export.IsEnabled=false;Error(e);}
        finally{IsEnabled=true;}
    }
    async Task Export()
    {
        if(generatedSettings==null)return;IsEnabled=false;status.Text="Writing body files, archive copies and previews…";
        try{string folder=await Task.Run(()=>Generator.Export(generatedSettings,generated));status.Text="Exported to "+folder;MessageBox.Show(this,"Export complete. Original game files were not modified.\n\n"+folder+"\n\nRead INSTALL.txt before testing in a copy of the game.","Export complete");}
        catch(Exception e){Error(e);}finally{IsEnabled=true;}
    }
    // ---- flat pictures ----
    static byte[] PaletteBytes(string folder)=>new Hqr(Path.Combine(folder,"RESS.HQR")).Read(0);
    BodyStyleStats StatsFor(int game,string folder)
    {
        if(styleStats.TryGetValue(game,out var known))return known;
        var hqr=new Hqr(Generator.BodyArchive(folder));var bodies=new List<byte[]>();
        for(int i=0;i<hqr.Count;i++){try{bodies.Add(hqr.Read(i));}catch(InvalidDataException){}}
        return styleStats[game]=BodyStyleStats.Analyse(game,bodies);
    }
    // The template's game and body index are the preview game's own fields (LBA1 / LBA2 installation and body index).
    void ExportSheet()=>Try(()=>
    {
        int game=previewGame.SelectedIndex+1;var s=ReadSettings();string folder=game==1?s.Lba1Folder:s.Lba2Folder;int index=game==1?s.Lba1Body:s.Lba2Body;
        var body=Body.Read(new Hqr(Generator.BodyArchive(folder)).Read(index),game);
        var sheet=FlatSheet.Render(body,PaletteBytes(folder));
        var d=new SaveFileDialog(){Filter="PNG image|*.png",FileName=$"lba{game}-body{index}-flat-sheet.png"};
        if(d.ShowDialog(this)!=true)return;
        FlatBitmap.Save(sheet,d.FileName);
        status.Text=$"Saved the game's own body {index} of LBA{game} as a flat front + back sheet ({FlatSheet.Colours(body).Length} colours, {body.Faces.Count} polygons): {d.FileName}. It is the kind of picture the generator reads.";
    });
    async Task ConvertStyle()
    {
        var s=ReadSettings();int game=previewGame.SelectedIndex+1;string folder=game==1?s.Lba1Folder:s.Lba2Folder;
        if(!File.Exists(s.ImagePath)){Error(new InvalidDataException("Choose a reference image first."));return;}
        var options=new StyleOptions(){Colours=Value(flatColours),Symmetrise=symmetric.IsChecked==true,BackFromFront=true};
        bool pair=pairImage.IsChecked==true;IsEnabled=false;status.Text="Reading the game's bodies and flattening the picture…";
        try
        {
            var (result,path)=await Task.Run(()=>
            {
                options.Allowed=StatsFor(game,folder).RecommendedDisplay(3);
                var image=FlatBitmap.Load(s.ImagePath);var palette=PaletteBytes(folder);
                var r=pair?GameStyle.ConvertPair(image,palette,options):GameStyle.Convert(image,palette,options);
                string directory=Path.Combine(string.IsNullOrWhiteSpace(s.OutputFolder)?AppContext.BaseDirectory:s.OutputFolder,"Flat sheets");Directory.CreateDirectory(directory);
                string file=Path.Combine(directory,Path.GetFileNameWithoutExtension(s.ImagePath)+$"-lba{game}-flat.png");FlatBitmap.Save(r.Sheet,file);
                return (r,file);
            });
            imagePath.Text=path;layout.SelectedItem="Front + back";mask.SelectedItem="Transparent background";LoadImage();
            status.Text=$"Flat sheet saved to {path} and set as the reference image (front + back, transparent background): {result.Notes}. Generate to build the body.";
        }
        catch(Exception e){Error(e);}
        finally{IsEnabled=true;}
    }
    void Try(Action action){try{action();}catch(Exception e){Error(e);}}
    void Error(Exception e){status.Text=e.Message;MessageBox.Show(this,e.Message,"Body Studio",MessageBoxButton.OK,MessageBoxImage.Error);}
}
