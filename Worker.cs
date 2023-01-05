namespace downloads_watcher
{
    public class Worker : BackgroundService
    {

        private ReaderWriterLockSlim rwlock;
        private FileSystemWatcher watcher;
        private FileSystemWatcher m4aWatcher;
    
        private const string _basePath = "C:/Users/tony/Downloads";
        private static readonly string moveDir = $"Z:/Music/{DateTime.Now.Year.ToString()}";

        private Queue<MyFile> queue = new();
        
        private bool moved;
        private bool isBusy = false;
        public Worker(ILogger<Worker> logger)
        {
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
            
            m4aWatcher = new FileSystemWatcher();
            m4aWatcher.Path = _basePath;
            m4aWatcher.Filter = "*.m4a";
            m4aWatcher.IncludeSubdirectories = false;
            m4aWatcher.EnableRaisingEvents = true;
            m4aWatcher.Created += OnCreated;
            
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine("[!] - File incoming");
            var myFile = new MyFile
            {
                Name = e.Name,
                FullPath = e.FullPath
            };
            
            queue.Enqueue(myFile); //add to queue
            
            if (!isBusy)
                ProcessQueue(queue.Peek()); //if not busy, process file
            
            
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
    
        private void ProcessQueue(MyFile item)
        {
            isBusy = true;
            while (IsFileLocked(new FileInfo(item.FullPath)))
                Thread.Sleep(100);

            moved = false;
            
            try
            {
                rwlock.EnterWriteLock();
                while (!moved)
                {
                    var moveToPath = Path.Combine(moveDir, item.Name);
                    File.Move(item.FullPath, moveToPath);
            
                    moved = File.Exists(moveToPath);
                    Console.WriteLine(moved ? "[!] - File successfully moved!" : "[x] - File has not been moved yet!");

                    if (moved)
                    {
                        queue.Dequeue();
                        isBusy = false;
                    }
                    Thread.Sleep(150);
                }
            }
            catch (Exception ex)
            {
                isBusy = false;
            }
            finally
            {
                if(rwlock.IsWriteLockHeld)
                    rwlock.ExitWriteLock();
            
                if(rwlock.IsReadLockHeld)
                    rwlock.ExitReadLock();
            }
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (queue.Any() && !isBusy)
                {
                    var item = queue.Peek();
                    ProcessQueue(item);
                }
            }
        }
    }

    internal class MyFile
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
    }
}