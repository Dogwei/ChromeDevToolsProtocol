

using ChromeDevToolsProtocol;
using System.Diagnostics;
using System.Text.Json;

const double MillimeterPerInche = 25.4;

using var chromeProcess = new ChromeProcess();

chromeProcess.SetToHeadlessMode();
// chromeProcess.SetUserDataDir(new DirectoryInfo($"D:/UserDataDir/{Guid.NewGuid():N}"));
// chromeProcess.SetRemoteDebugginPort(8586);

// chromeProcess.SetRemoteDebugginPort(8586);

const string HtmlFileFullName = @"E:/X/Desktop/面单设计/GC_KR_CJLogistics.html";

try
{
    chromeProcess.Start();

    var client = await chromeProcess.CreateRemoteClientAsync();

    await client.ConnectAsync();

    var createTargetResult = await client.Target.CreateTargetAsync(new TargetDomain.CreateTargetParams
    {
        Url = HtmlFileFullName,
    });

    var targetClient = client.CreateTargetClient(createTargetResult.TargetId);

    await targetClient.ConnectAsync();

    #region Browser.GetVersionAsync

    var version = await client.Browser.GetVersionAsync(new BrowserDomain.GetVersionParams { });

    Console.WriteLine(JsonSerializer.Serialize(version));

    #endregion

    #region GetWindowBounds

    var windowInfo = await client.Browser.GetWindowForTargetAsync(new BrowserDomain.GetWindowForTargetParams { TargetId = createTargetResult.TargetId });

    var windowBoundsResult = await client.Browser.GetWindowBoundsAsync(new BrowserDomain.GetWindowBoundsParams
    {
        WindowId = windowInfo.WindowId
    });

    Console.WriteLine(JsonSerializer.Serialize(windowBoundsResult));

    await client.Browser.SetWindowBoundsAsync(new BrowserDomain.SetWindowBoundsParams
    {
        WindowId = windowInfo.WindowId,
        Bounds = new BrowserDomain.Bounds
        {
            WindowState = BrowserDomain.WindowState.Maximized
        }
    });

    #endregion

    #region Runtime.AwaitPromiseAsync

    {
        var stopwatch = Stopwatch.StartNew();

        var promiseResult = await targetClient.Runtime.EvaluateAsync(new() { Expression = "(function (ms) { return new Promise((resolve) => setTimeout(() => resolve('1218'), ms)); })(1000)" });

        var awaitResult = await targetClient.Runtime.AwaitPromiseAsync(new() { PromiseObjectId = promiseResult.Result.ObjectId! });

        Console.WriteLine("used time:" + stopwatch.ElapsedMilliseconds + ",result:" + awaitResult.Result.Value);
    }

    #endregion

    #region Page.PrintToPDFAsync

    string? transferMode = null;

    var printToPDFResult = await targetClient.Page.PrintToPDFAsync(new PageDomain.PrintToPDFParams
    {
        PaperWidth = 150 / MillimeterPerInche,
        PaperHeight = 100 / MillimeterPerInche,
        MarginBottom = 0,
        MarginLeft = 0,
        MarginRight = 0,
        MarginTop = 0,
        PrintBackground = true,
        TransferMode = transferMode,
        PageRanges = "1"
    });

    var saveFileFullName = string.Concat(
        Path.GetDirectoryName(HtmlFileFullName),
        Path.DirectorySeparatorChar,
        Path.GetFileNameWithoutExtension(HtmlFileFullName),
        ".pdf"
        );

    if (transferMode == "ReturnAsStream")
    {
        int offset = 0;
        int size = 4096;

        var ms = new MemoryStream();

        while (true)
        {
            var readed = await targetClient.IO.ReadAsync(new() { Handle = printToPDFResult.Stream, Offset = offset, Size = size });

            Debug.Assert(readed.Base64Encoded == true);

            var readedBytes = Convert.FromBase64String(readed.Data);

            ms.Write(readedBytes);

            offset += readedBytes.Length;

            if (readed.Eof)
            {
                break;
            }
        }

        File.WriteAllBytes(saveFileFullName, ms.ToArray());
    }
    else
    {
        var bytes = Convert.FromBase64String(printToPDFResult.Data);

        File.WriteAllBytes(saveFileFullName, bytes);
    }

    #endregion

    #region Event

    await targetClient.Runtime.EnableAsync(new() { });

    targetClient.Runtime.ConsoleAPICalled += Runtime_ConsoleAPICalled;

    await targetClient.Runtime.EvaluateAsync(new() { Expression = "console.log('1218')" });

    targetClient.Runtime.ConsoleAPICalled -= Runtime_ConsoleAPICalled;

    await targetClient.Runtime.EvaluateAsync(new() { Expression = "console.log('1314')" });

    void Runtime_ConsoleAPICalled(object? sender, RuntimeDomain.ConsoleAPICalledParams e)
    {
        Console.WriteLine(JsonSerializer.Serialize(e.Args));
    }

    #endregion
}
finally
{
    chromeProcess.Kill();
}