using EspacoLivre;
var root=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());
Directory.CreateDirectory(Path.Combine(root,"a","b"));
try {
 File.WriteAllBytes(Path.Combine(root,"root.bin"),new byte[10]);
 File.WriteAllBytes(Path.Combine(root,"a","one.bin"),new byte[20]);
 File.WriteAllBytes(Path.Combine(root,"a","b","two.bin"),new byte[30]);
 Directory.CreateSymbolicLink(Path.Combine(root,"a","loop"),root);
 var result=Scanner.Run(root,null,CancellationToken.None);
 if(result.Bytes!=60 || result.Count!=3 || result.Folders.Count!=2 || result.Folders[0].Bytes!=50 || result.Files[0].Bytes!=30) throw new Exception("Aggregation or symlink exclusion failed");
 using var cancel=new CancellationTokenSource();cancel.Cancel();
 try {Scanner.Run(root,null,cancel.Token);throw new Exception("Cancellation failed");}catch(OperationCanceledException){}
 for(int i=0;i<1005;i++) File.WriteAllBytes(Path.Combine(root,$"file{i}"),new byte[i]);
 result=Scanner.Run(root,null,CancellationToken.None);
 if(result.Files.Count!=1000 || result.Files[0].Bytes!=1004 || result.Files[^1].Bytes!=8) throw new Exception("Top 1000 selection failed");
 Console.WriteLine("PASS: recursive sizes, ranking, symlink cycle exclusion, cancellation and top-1000 limit.");
} finally {Directory.Delete(root,true);}
