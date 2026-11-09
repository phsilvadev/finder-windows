using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualBasic.FileIO;
namespace EspacoLivre;
public partial class MainWindow : Window {
    private CancellationTokenSource? cancellation;
    private List<Entry> mapEntries = [];
    private readonly string[] palette = ["#04A5AC", "#1675E9", "#8255D6", "#B64AA3", "#C49D25", "#557C9F"];
    private long driveUsed;

    public MainWindow() { InitializeComponent(); PathBox.Text=Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\"; UpdateDrive(); }
    private void UpdateDrive() {
        try { var drive=new DriveInfo(Path.GetPathRoot(PathBox.Text)!); DriveText.Text=$"{drive.Name} • {Entry.Format(drive.TotalSize-drive.TotalFreeSpace)} usados • {Entry.Format(drive.AvailableFreeSpace)} livres"; DriveBar.Value=100.0*(drive.TotalSize-drive.TotalFreeSpace)/drive.TotalSize;
            driveUsed=drive.TotalSize-drive.TotalFreeSpace;
            UsedText.Text=$"{Entry.Format(driveUsed)} usados";
            FreeText.Text=$"{Entry.Format(drive.AvailableFreeSpace)} livres de {Entry.Format(drive.TotalSize)}";
            DrawSegments(drive.TotalSize, driveUsed); }
        catch { DriveText.Text="Selecione uma pasta local"; DriveBar.Value=0; }
    }
    private void Choose_Click(object sender,RoutedEventArgs e) { var dialog=new Microsoft.Win32.OpenFolderDialog { Title="Escolha a pasta ou unidade" }; if(dialog.ShowDialog()==true) {PathBox.Text=dialog.FolderName;UpdateDrive();} }
    private async void Scan_Click(object sender,RoutedEventArgs e) => await Scan();
    private async Task Scan() {
        if(cancellation!=null) return;
        string root;
        try {root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(PathBox.Text)); if(!Directory.Exists(root)) throw new IOException("A pasta não existe.");}
        catch(Exception ex) {MessageBox.Show(ex.Message,"Pasta inválida");return;}
        cancellation=new(); SetBusy(true); UpdateDrive(); FolderGrid.ItemsSource=null;FileGrid.ItemsSource=null; mapEntries=[]; MapCanvas.Children.Clear();TopFoldersPanel.Children.Clear();MapEmpty.Visibility=FoldersEmpty.Visibility=Visibility.Visible;SummaryText.Text="Analisando a pasta selecionada…";
        var progress=new Progress<string>(s=>StatusText.Text=s);
        try { var result=await Task.Run(()=>Scanner.Run(root,progress,cancellation.Token));
            FolderGrid.ItemsSource=result.Folders;FileGrid.ItemsSource=result.Files;
            PopulateOverview(result,root);
            SummaryText.Text=$"{result.Count:N0} arquivos • {Entry.Format(result.Bytes)} em tamanho lógico • {result.Skipped:N0} erros de acesso";
            StatusText.Text="Concluído • Até 500 pastas e 1.000 maiores arquivos. Subpastas estão incluídas no tamanho das pastas pais.";
        } catch(OperationCanceledException) {StatusText.Text="Análise cancelada. Inicie novamente para obter os resultados.";}
        catch(Exception ex) {StatusText.Text="Falha na análise: "+ex.Message;}
        finally {cancellation!.Dispose();cancellation=null;SetBusy(false);}
    }
    private void SetBusy(bool busy) {ScanButton.IsEnabled=ChooseButton.IsEnabled=PathBox.IsEnabled=OpenButton.IsEnabled=TrashButton.IsEnabled=!busy;CancelButton.IsEnabled=busy;CancelButton.Visibility=busy?Visibility.Visible:Visibility.Collapsed;ScanProgress.Visibility=busy?Visibility.Visible:Visibility.Collapsed;}
    private void Cancel_Click(object sender,RoutedEventArgs e)=>cancellation?.Cancel();
    private void Folder_DoubleClick(object sender,System.Windows.Input.MouseButtonEventArgs e) {if(cancellation==null && FolderGrid.SelectedItem is Entry entry) {PathBox.Text=entry.Path;_=Scan();}}
    private void Open_Click(object sender,RoutedEventArgs e) {if(FileGrid.SelectedItem is Entry entry) Launch("explorer.exe",$"/select,\"{entry.Path}\"");}
    private static void Launch(string file,string args="") {try {Process.Start(new ProcessStartInfo(file,args){UseShellExecute=true});} catch(Exception ex) {MessageBox.Show(ex.Message,"Não foi possível abrir");}}
    private static bool Protected(string path) {
        string full=Path.GetFullPath(path);
        string[] roots=[Environment.GetFolderPath(Environment.SpecialFolder.Windows),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)];
        return roots.Where(r=>!string.IsNullOrWhiteSpace(r)).Any(r=>full.StartsWith(r.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase)) || full.Contains("\\$Recycle.Bin\\",StringComparison.OrdinalIgnoreCase) || full.Contains("\\System Volume Information\\",StringComparison.OrdinalIgnoreCase);
    }
    private async void Trash_Click(object sender,RoutedEventArgs e) {
        if(cancellation!=null || FileGrid.SelectedItem is not Entry entry) return;
        try {
            if(Protected(entry.Path) || (File.GetAttributes(entry.Path)&(FileAttributes.System|FileAttributes.ReparsePoint))!=0) {MessageBox.Show("Este arquivo está em uma área protegida. Use as configurações do Windows para gerenciá-lo.","Arquivo protegido");return;}
            if(MessageBox.Show($"Enviar à Lixeira?\n\n{entry.Path}\n{entry.SizeText}\n\nVerifique se o arquivo não é necessário para seus aplicativos.","Revisar exclusão",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes) return;
            FileSystem.DeleteFile(entry.Path,UIOption.AllDialogs,RecycleOption.SendToRecycleBin,UICancelOption.ThrowException);
            await Scan();
        } catch(OperationCanceledException) {StatusText.Text="Exclusão cancelada.";} catch(Exception ex) {MessageBox.Show(ex.Message,"Não foi possível enviar à Lixeira");}
    }
    private async void Downloads_Click(object sender,RoutedEventArgs e) {if(cancellation!=null)return;PathBox.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads");await Scan();}
    private async void Temp_Click(object sender,RoutedEventArgs e) {if(cancellation!=null)return;PathBox.Text=Path.GetTempPath();await Scan();}
    private void Storage_Click(object sender,RoutedEventArgs e)=>Launch("ms-settings:storagesense");

    private void Navigate_Click(object sender,RoutedEventArgs e) {
        if(OverviewPanel==null) return;
        string page=(string)((RadioButton)sender).Tag;
        OverviewPanel.Visibility=page=="0"?Visibility.Visible:Visibility.Collapsed;
        FilesPanel.Visibility=page=="1"?Visibility.Visible:Visibility.Collapsed;
        FolderListPanel.Visibility=page=="2"?Visibility.Visible:Visibility.Collapsed;
        CleanupPanel.Visibility=page is "0" or "3"?Visibility.Visible:Visibility.Collapsed;
        SettingsPanel.Visibility=page=="4"?Visibility.Visible:Visibility.Collapsed;
    }
    private void Minimize_Click(object sender,RoutedEventArgs e)=>WindowState=WindowState.Minimized;
    private void Maximize_Click(object sender,RoutedEventArgs e)=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
    private void Close_Click(object sender,RoutedEventArgs e)=>Close();
    private void Recycle_Click(object sender,RoutedEventArgs e)=>Launch("explorer.exe","shell:RecycleBinFolder");
    private static Brush Paint(string color)=>(Brush)new BrushConverter().ConvertFromString(color)!;
    private void PopulateOverview(ScanResult result,string root) {
        var children=result.TopLevelFolders.Where(x=>x.Bytes>0).ToList();
        mapEntries=children.Take(5).ToList();
        long remainder=Math.Max(0,result.Bytes-mapEntries.Sum(x=>x.Bytes));
        if(remainder>0) mapEntries.Add(new Entry("Outros arquivos e pastas",remainder));
        MapEmpty.Visibility=mapEntries.Count==0?Visibility.Visible:Visibility.Collapsed;
        FoldersEmpty.Visibility=children.Count==0?Visibility.Visible:Visibility.Collapsed;
        foreach(var item in children.Take(4)) {
            var row=new Grid {Margin=new Thickness(0,5,0,13)};
            row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(42)});
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            row.Children.Add(new TextBlock {Text="\uE8B7",FontFamily=new FontFamily("Segoe MDL2 Assets"),FontSize=28,Foreground=Paint("#FFD05D"),VerticalAlignment=VerticalAlignment.Center});
            var content=new StackPanel {Margin=new Thickness(8,0,15,0)};Grid.SetColumn(content,1);
            content.Children.Add(new TextBlock {Text=item.Name,FontSize=16,TextTrimming=TextTrimming.CharacterEllipsis,ToolTip=item.Path});
            content.Children.Add(new ProgressBar {Value=100.0*item.Bytes/Math.Max(1,children[0].Bytes),Height=9,Margin=new Thickness(0,8,0,0),Foreground=Paint("#279BFF"),Background=Paint("#283F5E"),BorderThickness=new Thickness(0)});
            row.Children.Add(content);
            var size=new TextBlock {Text=item.SizeText,FontSize=15,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(size,2);row.Children.Add(size);
            var button=new Button {Content=row,Padding=new Thickness(0,3,0,3),Background=Brushes.Transparent,BorderThickness=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Stretch,ToolTip="Analisar "+item.Path};
            button.Click+=async (_,_)=>{if(cancellation!=null)return;PathBox.Text=item.Path;await Scan();};TopFoldersPanel.Children.Add(button);
        }
        DrawMap();
        // The drive bar shows physical occupancy. Folder results have logical sizes and a separate legend.
        LegendPanel.Children.Clear();
        for(int i=0;i<mapEntries.Count;i++) {
            var label=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,24,6)};
            label.Children.Add(new Border {Background=Paint(palette[i]),Width=15,Height=15,CornerRadius=new CornerRadius(4),Margin=new Thickness(0,0,8,0)});
            label.Children.Add(new TextBlock {Text=$"{mapEntries[i].Name}  {mapEntries[i].SizeText}",FontSize=13});LegendPanel.Children.Add(label);
        }
        DriveText.Text=$"Pasta analisada: {root} • Cores do mapa: tamanhos lógicos das pastas";
    }
    private void DrawSegments(long capacity,long used) {
        StorageSegments.Children.Clear();StorageSegments.ColumnDefinitions.Clear();
        StorageSegments.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(Math.Max(1,used),GridUnitType.Star)});
        StorageSegments.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(Math.Max(1,capacity-used),GridUnitType.Star)});
        var fill=new Border {Background=new LinearGradientBrush(((SolidColorBrush)Paint("#1682FA")).Color,((SolidColorBrush)Paint("#27C7C0")).Color,0)};
        StorageSegments.Children.Add(fill);
        if(LegendPanel.Children.Count==0) {LegendPanel.Children.Add(new TextBlock {Text="Espaço ocupado na unidade • Analise para ver a distribuição por pastas",Foreground=Paint("#A7B6CF"),FontSize=13});}
    }
    private void Map_SizeChanged(object sender,SizeChangedEventArgs e)=>DrawMap();
    private void DrawMap() {
        if(MapCanvas==null) return;
        MapCanvas.Children.Clear();
        if(mapEntries.Count==0 || MapCanvas.ActualWidth<=0) return;
        PlaceTiles(0,mapEntries.Count,0,0,MapCanvas.ActualWidth,MapCanvas.ActualHeight);
    }
    private void PlaceTiles(int start,int count,double x,double y,double width,double height) {
        if(count==1) {
            var entry=mapEntries[start];
            var text=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
            text.Children.Add(new TextBlock {Text="\uE8B7",FontFamily=new FontFamily("Segoe MDL2 Assets"),FontSize=25,Margin=new Thickness(0,0,0,8)});
            text.Children.Add(new TextBlock {Text=entry.Name,FontSize=19,FontWeight=FontWeights.SemiBold,TextTrimming=TextTrimming.CharacterEllipsis});
            text.Children.Add(new TextBlock {Text=entry.SizeText,FontSize=20,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,4,0,0)});
            var tile=new Button {Content=text,Background=Paint(palette[start%palette.Length]),BorderBrush=Paint("#72B5D9"),Padding=new Thickness(16),Width=Math.Max(0,width-5),Height=Math.Max(0,height-5),HorizontalContentAlignment=HorizontalAlignment.Stretch,ToolTip=$"{entry.Name} • {entry.SizeText}",ClipToBounds=true};
            if(start<5 && Directory.Exists(entry.Path)) tile.Click+=async (_,_)=>{if(cancellation!=null)return;PathBox.Text=entry.Path;await Scan();};
            Canvas.SetLeft(tile,x);Canvas.SetTop(tile,y);MapCanvas.Children.Add(tile);return;
        }
        double total=mapEntries.Skip(start).Take(count).Sum(i=>(double)i.Bytes),sum=0;int split=1;
        for(int i=1;i<count;i++) {sum+=mapEntries[start+i-1].Bytes;split=i;if(sum>=total/2)break;}
        double ratio=sum/total;
        if(width>=height) {PlaceTiles(start,split,x,y,width*ratio,height);PlaceTiles(start+split,count-split,x+width*ratio,y,width*(1-ratio),height);}
        else {PlaceTiles(start,split,x,y,width,height*ratio);PlaceTiles(start+split,count-split,x,y+height*ratio,width,height*(1-ratio));}
    }

    protected override void OnClosed(EventArgs e) {cancellation?.Cancel();base.OnClosed(e);}
}