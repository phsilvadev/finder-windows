using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.VisualBasic.FileIO;
namespace EspacoLivre;
public partial class MainWindow : Window {
    private CancellationTokenSource? cancellation;
    public MainWindow() { InitializeComponent(); PathBox.Text=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); UpdateDrive(); }
    private void UpdateDrive() {
        try { var drive=new DriveInfo(Path.GetPathRoot(PathBox.Text)!); DriveText.Text=$"{drive.Name} • {Entry.Format(drive.TotalSize-drive.TotalFreeSpace)} usados • {Entry.Format(drive.AvailableFreeSpace)} livres"; DriveBar.Value=100.0*(drive.TotalSize-drive.TotalFreeSpace)/drive.TotalSize; }
        catch { DriveText.Text="Selecione uma pasta local"; DriveBar.Value=0; }
    }
    private void Choose_Click(object sender,RoutedEventArgs e) { var dialog=new Microsoft.Win32.OpenFolderDialog { Title="Escolha a pasta ou unidade" }; if(dialog.ShowDialog()==true) {PathBox.Text=dialog.FolderName;UpdateDrive();} }
    private async void Scan_Click(object sender,RoutedEventArgs e) => await Scan();
    private async Task Scan() {
        if(cancellation!=null) return;
        string root;
        try {root=Path.GetFullPath(PathBox.Text); if(!Directory.Exists(root)) throw new IOException("A pasta não existe.");}
        catch(Exception ex) {MessageBox.Show(ex.Message,"Pasta inválida");return;}
        cancellation=new(); SetBusy(true); UpdateDrive(); FolderGrid.ItemsSource=null;FileGrid.ItemsSource=null;
        var progress=new Progress<string>(s=>StatusText.Text=s);
        try { var result=await Task.Run(()=>Scanner.Run(root,progress,cancellation.Token));
            FolderGrid.ItemsSource=result.Folders;FileGrid.ItemsSource=result.Files;
            SummaryText.Text=$"{result.Count:N0} arquivos • {Entry.Format(result.Bytes)} em tamanho lógico • {result.Skipped:N0} erros de acesso";
            StatusText.Text="Concluído • Até 500 pastas e 1.000 maiores arquivos. Subpastas estão incluídas no tamanho das pastas pais.";
        } catch(OperationCanceledException) {StatusText.Text="Análise cancelada. Inicie novamente para obter os resultados.";}
        catch(Exception ex) {StatusText.Text="Falha na análise: "+ex.Message;}
        finally {cancellation.Dispose();cancellation=null;SetBusy(false);}
    }
    private void SetBusy(bool busy) {ScanButton.IsEnabled=ChooseButton.IsEnabled=PathBox.IsEnabled=OpenButton.IsEnabled=TrashButton.IsEnabled=!busy;CancelButton.IsEnabled=busy;ScanProgress.Visibility=busy?Visibility.Visible:Visibility.Collapsed;}
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
    protected override void OnClosed(EventArgs e) {cancellation?.Cancel();base.OnClosed(e);}
}