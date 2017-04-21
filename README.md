# EasySyncClient
A Dropbox-like for Alfresco ECM

This sofware extend Alfresco ECM with offline sync capabilities similar to what Dropbox / OneDrive / etc... do.

The software is made in C# and requires .NET Framework 4.5.2 to run on Windows.

It has been fully tested on Windows and being validated for MacOs.

The software is using :
  - dotCMIS library -> https://chemistry.apache.org/dotnet/dotcmis.html
  - DynamicLogViewer -> http://tringi.trimcore.cz/Dynamic_Log_Viewer
  - SQLite -> https://www.sqlite.org/
  - Mono (for the MacOs version) -> http://www.mono-project.com/

For more information : http://ks2.fr/produits/ks2-easysync-client-en/

#How does it work<br/>
1 - Donwload the latest release<br/>
2 - Run Easysyncclient.exe<br/>
3 - You will be prompted to enter your Alfresco's credential as well a your Alfresco's CMIS endpoint URL (this URL is http://YOUR_ALFRESO_SERVER_NAME_OR_IP/alfresco/cmisatom) <br/>
4 - Select the site you want to synchronize<br/>
5 - Select the local folder on your workstation where to synchonize<br/>
6 - That's it ! You're syncing !
