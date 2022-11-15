# ChromeDevToolsProtocol
Chrome Devtools Protocol client implementation with full API. based Source Generator.

#### Install Chrome on CentOS

```
wget https://dl.google.com/linux/direct/google-chrome-stable_current_x86_64.rpm


yum install ./google-chrome-stable_current_x86_64.rpm
```

#### PrintToPdf

```C#

var chromeProcess = new ChromeProcess();

chromeProcess.SetToHeadlessMode();
chromeProcess.SetArg("--no-sandbox");

var started = _chromeProcess.Start();


var baseRemoteClient = await chromeProcess.CreateRemoteClientAsync();

await baseRemoteClient.ConnectAsync();


var targetClient = await baseRemoteClient.CreateTargetClientAsync(new TargetDomain.CreateTargetParams
{

    Url = "file:///yourlocalfile.html",

};


await targetClient.ConnectAsync();


var printToPDFResult = await targetClient.Page.PrintToPDFAsync(new PageDomain.PrintToPDFParams
{
    PaperWidth = 4,
    PaperHeight = 6,
    PageRanges = "1",

    MarginBottom = 0,
    MarginLeft = 0,
    MarginRight = 0,
    MarginTop = 0,

    PrintBackground = true,


}, cancellationToken: new CancellationTokenSource(PrintTimeout).Token);

```
