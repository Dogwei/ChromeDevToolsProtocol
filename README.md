# ChromeDevToolsProtocol
Chrome Devtools Protocol client implementation with full API. based Source Generator.

### If you want to use ChromeDevToolsProtocol, please download or install the latest version on [Nuget](https://www.nuget.org/packages/ChromeDevToolsProtocol).
### Please make sure your machine has the official Chrome program installed before use.


#### Advantages

- Cross-platform.
- No native libraries required(Except Chrome itself).
- Contains all remote debugging APIs provided by Chrome.

#### PrintToPdf

```C#

var chromeProcess = new ChromeProcess();

chromeProcess.SetToHeadlessMode();
chromeProcess.SetArg("--no-sandbox");

var started = chromeProcess.Start();


var baseRemoteClient = await chromeProcess.CreateRemoteClientAsync();

await baseRemoteClient.ConnectAsync();


var targetClient = await baseRemoteClient.CreateTargetClientAsync(new ()
{

    Url = "yourhtmlfileurl.html",

};


await targetClient.ConnectAsync();


var printToPDFResult = await targetClient.Page.PrintToPDFAsync(new ()
{
    PaperWidth = 4,
    PaperHeight = 6,
    PageRanges = "1",

    MarginBottom = 0,
    MarginLeft = 0,
    MarginRight = 0,
    MarginTop = 0,

    PrintBackground = true,


});

```
