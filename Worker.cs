namespace downloads_watcher;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    private ReaderWriterLockSlim rwlock;
    private FileSystemWatcher watcher;
    private Timer processTimer;
    
    private const string _basePath = "C:/Users/tony/Downloads/temp2";
    private const string moveDir = "C:/Users/tony/Downloads/temp3";
    private List<MyFile> paths = new List<MyFile>();
    private bool moved = false;
    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
        Initialize();
    }

    private void Initialize()
    {
        rwlock = new ReaderWriterLockSlim();

        watcher = new FileSystemWatcher();
        watcher.Path = _basePath;
        watcher.Filter = "*.mp3";
        watcher.IncludeSubdirectories = false;
        watcher.EnableRaisingEvents = true;
        watcher.Created += OnCreated;
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        Console.WriteLine("[!] - Incoming file");
        var myFile = new MyFile
        {
            Name = e.Name,
            FullPath = e.FullPath
        };
        
        paths.Add(myFile);

        while (IsFileLocked(new FileInfo(myFile.FullPath)))
        {
            Console.WriteLine("[!] - Waiting for file to finish downloading");
            Thread.Sleep(2500);
        }
        
        Console.WriteLine("[!] - Download finished. Start moving file");
        moved = false;
        processTimer = null;
        try
        {
            rwlock.EnterWriteLock();
            while (!moved)
            {
                if (processTimer == null)
                {
                    Console.WriteLine("timer has been started");
                    processTimer = new Timer(ProcessQueue, null, TimeSpan.Zero, TimeSpan.Zero);
                }
                else
                {
                    Console.WriteLine("Timer was not null");
                    Thread.Sleep(2500);
                    processTimer = null;
                }
                Thread.Sleep(1000);
            }
            

        }
        catch (Exception ex)
        {
            // ignored
        }
        finally
        {
            if(rwlock.IsWriteLockHeld)
                rwlock.ExitWriteLock();
            
            if(rwlock.IsReadLockHeld)
                rwlock.ExitReadLock();
        }
    }
    private bool IsFileLocked(FileInfo file)
    {
        FileStream stream = null;

        try
        {
            stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            return true;
        }
        finally
        {
            if (stream != null)
                stream.Close();
        }
        //file is not locked
        return false;
    }
    
    private void ProcessQueue(object? sender)
    {
        try
        {
            Console.WriteLine("[!] - Moving incoming file..");
            //rwlock.EnterReadLock();
            
            var toMove = paths.First();
            var moveToPath = Path.Combine(moveDir, toMove.Name);
            
            File.Move(toMove.FullPath, moveToPath);
            
            moved = File.Exists(moveToPath);
            Console.WriteLine(moved ? "[!] - File successfully moved!" : "[x] - File has not been moved yet!");
        }
        catch (Exception err)
        {
            Console.WriteLine("[x] - Error occured while moving");
            Console.WriteLine(err);
        }
        finally
        {
            if(rwlock.IsWriteLockHeld)
                rwlock.ExitWriteLock();
            
            if (rwlock.IsReadLockHeld)
                rwlock.ExitReadLock();
            
            paths.Clear();
        }
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        //ignored
    }
}

class MyFile
{
    public string Name { get; set; }
    public string FullPath { get; set; }
}