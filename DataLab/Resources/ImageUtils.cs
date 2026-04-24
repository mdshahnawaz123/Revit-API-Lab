using System;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DataLab.Resources
{
    public static class ImageUtils
    {
        public static ImageSource GetEmbeddedImage(string resourceName, Assembly sourceAssembly = null)
        {
            // ✅ Load from provided assembly, or fallback to THIS assembly (DataLab)
            Assembly assembly = sourceAssembly ?? typeof(ImageUtils).Assembly;

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