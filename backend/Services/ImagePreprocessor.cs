using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace WebApplication1.Services
{
    public class ImagePreprocessor
    {
        // Normalize: fix EXIF orientation and improve OCR contrast
        public void Normalize(Image<Rgba32> img)
        {
            img.Mutate(x =>
            {
                x.AutoOrient();
                x.Contrast(1.2f);
                x.Brightness(1.1f);
                x.Grayscale();
            });
        }

        // Auto-crop to content using a simple brightness threshold scan
        // Finds the bounding box of non-background pixels and crops with padding.
        public void AutoCrop(Image<Rgba32> img, byte backgroundThreshold = 245, int padding = 10)
        {
            // Work on a downscaled grayscale clone for speed
            int sampleW = Math.Max(1, img.Width / 4);
            int sampleH = Math.Max(1, img.Height / 4);
            using var gray = img.CloneAs<L8>();
            if (gray.Width > sampleW || gray.Height > sampleH)
            {
                gray.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(sampleW, sampleH)
                }));
            }

            int w = gray.Width, h = gray.Height;
            int top = 0, bottom = h - 1, left = 0, right = w - 1;

            bool RowHasContent(int y)
            {
                gray.ProcessPixelRows(accessor =>
                {
                    var row = accessor.GetRowSpan(y);
                    int dark = 0;
                    int limit = Math.Max(1, (int)(w * 0.02)); // at least 2% pixels are non-background
                    for (int x = 0; x < w; x++)
                    {
                        if (row[x].PackedValue < backgroundThreshold) dark++;
                        if (dark >= limit) { limit = -1; break; }
                    }
                    if (limit == -1) _flag = true; else _flag = false;
                });
                return _flag;
            }
            bool ColHasContent(int x)
            {
                int dark = 0;
                int limit = Math.Max(1, (int)(h * 0.02));
                gray.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < h; y++)
                    {
                        if (accessor.GetRowSpan(y)[x].PackedValue < backgroundThreshold) dark++;
                        if (dark >= limit) { _flag = true; return; }
                    }
                    _flag = false;
                });
                return _flag;
            }

            // Small trick to pass bool from local lambdas
            _flag = false;

            // find top
            for (int y = 0; y < h; y++) { if (RowHasContent(y)) { top = y; break; } }
            // bottom
            for (int y = h - 1; y >= 0; y--) { if (RowHasContent(y)) { bottom = y; break; } }
            // left
            for (int x = 0; x < w; x++) { if (ColHasContent(x)) { left = x; break; } }
            // right
            for (int x = w - 1; x >= 0; x--) { if (ColHasContent(x)) { right = x; break; } }

            // Map back to original resolution
            float scaleX = (float)img.Width / w;
            float scaleY = (float)img.Height / h;
            int x1 = Math.Max(0, (int)Math.Floor(left * scaleX) - padding);
            int y1 = Math.Max(0, (int)Math.Floor(top * scaleY) - padding);
            int x2 = Math.Min(img.Width, (int)Math.Ceiling((right + 1) * scaleX) + padding);
            int y2 = Math.Min(img.Height, (int)Math.Ceiling((bottom + 1) * scaleY) + padding);

            int cropW = Math.Max(1, x2 - x1);
            int cropH = Math.Max(1, y2 - y1);

            // Only crop if it meaningfully reduces area
            if (cropW < img.Width - 8 && cropH < img.Height - 8)
            {
                img.Mutate(x => x.Crop(new Rectangle(x1, y1, cropW, cropH)));
            }
        }

        // internal flag for lambda communication
        private bool _flag;
    }
}