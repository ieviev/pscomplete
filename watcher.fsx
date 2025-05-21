open System.IO
open System

let watcher =
    new FileSystemWatcher(".", "*.fs", EnableRaisingEvents = true, IncludeSubdirectories = true)

let rec loop fn (nextproctime: DateTimeOffset) : unit =
    let changed = watcher.WaitForChanged WatcherChangeTypes.Changed

    match DateTimeOffset.Now > nextproctime with
    | false -> loop fn (nextproctime)
    | true ->
        fn changed
        loop fn (nextproctime.AddSeconds(2))


DateTimeOffset.Now
|> loop (fun v ->
    stdout.WriteLine "rebuilding.."
    use v = Process.mirror ("pwsh", "build.ps1")
    v |> Task.await
    ()
)

