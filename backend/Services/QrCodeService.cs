using QRCoder;

namespace WebApplication1.Services
{
    public static class QrCodeService
    {
        public static string ToDataUri(string text, int pixelsPerModule = 8)
        {
            var gen = new QRCodeGenerator();
            var data = gen.CreateQrCode(text ?? string.Empty, QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(data);
            var bytes = png.GetGraphic(pixelsPerModule);
            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        }
    }
}