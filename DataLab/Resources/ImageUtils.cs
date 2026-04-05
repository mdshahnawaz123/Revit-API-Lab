using System;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DataLab.Resources
{
    public static class ImageUtils
    {
        public static ImageSource GetEmbeddedImage(string resourceName)
        {
            // ✅ Always load from THIS assembly (DataLab)
            Assembly assembly = typeof(ImageUtils).Assembly;

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new Exception("Image not found: " + resourceName);

                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = stream;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();

                return image;
            }
        }
    }
}