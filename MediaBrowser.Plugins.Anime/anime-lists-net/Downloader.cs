using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace AnimeLists
{
    public class Downloader
    {
        private readonly AsyncLock _lock = new AsyncLock();
        private readonly string _temp;

        public Downloader(string temp)
        {
            _temp = temp;
        }

        public async Task<Animelist> Download()
        {
            using (await _lock.LockAsync())
            {
                var info = new FileInfo(_temp);
                if (!info.Exists || info.LastWriteTimeUtc < (DateTime.UtcNow - TimeSpan.FromDays(7)))
                {
                    if (info.Exists)
                        info.Delete();

                    WebClient client = new WebClient();
                    await client.DownloadFileTaskAsync("https://raw.githubusercontent.com/ScudLee/anime-lists/master/anime-list.xml", _temp);
                }
            }

            XmlSerializer serializer = new XmlSerializer(typeof(Animelist));

            using (var stream = File.OpenRead(_temp))
                return serializer.Deserialize(stream) as Animelist;
        }
    }
}