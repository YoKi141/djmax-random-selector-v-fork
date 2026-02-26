using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace DjmaxRandomSelectorV
{
    public class UpdateManager
    {
        private const string VersionCheckUrl = "https://raw.githubusercontent.com/YoKi141/djmax-random-selector-v/main/Version3.txt";
        private const string AllTrackDownloadUrl = "https://v-archive.net/db/songs.json";
        private const string AllTrackFilePath = @"DMRSV3_Data\AllTrackList.json";

        private readonly VersionContainer _container;
        private readonly IFileManager _fileManager;

        public UpdateManager(Dmrsv3Configuration config, VersionContainer container, IFileManager fileManager)
        {
            _container = container;
            _fileManager = fileManager;
            Version assemblyVersion = Assembly.GetEntryAssembly().GetName().Version;
            _container.CurrentAppVersion = _container.LatestAppVersion = assemblyVersion;
            _container.AllTrackVersion = config.AllTrackVersion;
        }

        public async Task UpdateAsync()
        {
            string latestAppVersion;
            try
            {
                string result = await _fileManager.RequestAsync(VersionCheckUrl);
                latestAppVersion = result.Split('\n')[0];
            }
            catch
            {
                throw new Exception("Failed to check update.");
            }
            _container.LatestAppVersion = new Version(latestAppVersion);

            // Update AllTrackList.json if stale or missing
            long now = long.Parse(DateTime.Now.ToString("yyMMddHHmm"));
            long past = _container.AllTrackVersion;
            if (now > past || !File.Exists(AllTrackFilePath))
            {
                Debug.WriteLine("all track update start");
                int result = await DownloadAllTrackAsync();
                if (result == 0)
                {
                    _container.AllTrackVersion = now;
                }
            }
        }

        private async Task<int> DownloadAllTrackAsync()
        {
            try
            {
                string result = await _fileManager.RequestAsync(AllTrackDownloadUrl);
                _fileManager.Write(result, AllTrackFilePath);
            }
            catch
            {
                return -1;
            }
            return 0;
        }
    }
}
