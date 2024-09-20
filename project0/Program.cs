namespace CS120
{
class Program
{
    static readonly string audioFile = "assets/初星学園,ギガP,花海咲季 - Fighting My Way - Instrumental.mp3";
    static readonly string recordFileName = "assets/recorded.wav";
    [STAThread]
    static void Main()
    {
        AudioManager.ListAsioDevice().ToList().ForEach(x => Console.WriteLine(x));
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        var manager = new AudioManager();
        AsyncTask(manager, cts.Token).GetAwaiter().GetResult();
    }

    static async Task AsyncTask(AudioManager manager, CancellationToken ct)
    {
        // await manager.Play(audioFile, "ASIO4ALL v2");
        using var audio_cts = new CancellationTokenSource();

        var task = manager.RecordAndPlay(recordFileName, "ASIO4ALL v2", audio_cts.Token, audioFile: audioFile);

        var delay_task = Task.Delay(10000, ct);

        if (await Task.WhenAny(task, delay_task) == delay_task)
        {
            audio_cts.Cancel();
        }
        await task;
    }
}
}