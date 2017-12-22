namespace Disk.SDK.Utils
{
    public class ResourceProvider
    {
        public static string Get(string key)
        {
            return WebdavResources.ResourceManager.GetString(key, WebdavResources.Culture);
        }
    }
}
