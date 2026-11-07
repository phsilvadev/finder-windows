using System.IO;
namespace EspacoLivre;
public record Entry(string Path, long Bytes) {
    public string Name => System.IO.Path.GetFileName(Path.TrimEnd(System.IO.Path.DirectorySeparatorChar));
    public string SizeText => Format(Bytes);
    public static string Format(long n) { string[] units = ["B", "KB", "MB", "GB", "TB"]; double v=n; int i=0; while(v>=1024 && i<4){v/=1024;i++;} return $"{v:N1} {units[i]}"; }
}
public record ScanResult(List<Entry> Folders, List<Entry> Files, long Bytes, long Count, long Skipped);
public static class Scanner {
    public static ScanResult Run(string root, IProgress<string>? progress, CancellationToken token) {
        var folders=new List<Entry>(); var largest=new PriorityQueue<Entry,long>(); long total=0,count=0,skipped=0;
        var options=new EnumerationOptions { IgnoreInaccessible=true, AttributesToSkip=FileAttributes.ReparsePoint, RecurseSubdirectories=false };
        var stack=new Stack<(string Path,bool Visited)>(); var sizes=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
        var clock=System.Diagnostics.Stopwatch.StartNew(); stack.Push((root,false));
        while(stack.TryPop(out var item)) {
            token.ThrowIfCancellationRequested();
            if(item.Visited) {
                long size=sizes[item.Path];
                if(!string.Equals(item.Path,root,StringComparison.OrdinalIgnoreCase)) {
                    folders.Add(new(item.Path,size)); var parent=Directory.GetParent(item.Path)?.FullName;
                    if(parent!=null && sizes.ContainsKey(parent)) sizes[parent]+=size;
                }
                continue;
            }
            sizes[item.Path]=0; stack.Push((item.Path,true));
            try {
                foreach(var path in Directory.EnumerateFiles(item.Path,"*",options)) {
                    token.ThrowIfCancellationRequested();
                    try { var info=new FileInfo(path); long bytes=info.Length; sizes[item.Path]+=bytes;total+=bytes;count++;
                        largest.Enqueue(new(path,bytes),bytes); if(largest.Count>1000) largest.Dequeue();
                    } catch(Exception e) when(e is IOException or UnauthorizedAccessException) {skipped++;}
                }
                foreach(var dir in Directory.EnumerateDirectories(item.Path,"*",options)) stack.Push((dir,false));
            } catch(Exception e) when(e is IOException or UnauthorizedAccessException) {skipped++;}
            if(clock.ElapsedMilliseconds>150) {progress?.Report($"{count:N0} arquivos • {Entry.Format(total)} • {item.Path}");clock.Restart();}
        }
        return new(folders.OrderByDescending(x=>x.Bytes).Take(500).ToList(),largest.UnorderedItems.Select(x=>x.Element).OrderByDescending(x=>x.Bytes).ToList(),total,count,skipped);
    }
}
