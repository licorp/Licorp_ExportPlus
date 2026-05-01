using Autodesk.Revit.DB;

namespace LicorpExportPlus.Models
{
    public class ImageExportSettings
    {
        public string OutputFolder { get; set; }
        public ImageFileType ImageFormat { get; set; } = ImageFileType.PNG;
        public ImageResolution Resolution { get; set; } = ImageResolution.DPI_300;
        public bool UseCustomPixelSize { get; set; } = false;
        public int PixelSize { get; set; } = 1920;
        public int ZoomPercentage { get; set; } = 100;
        public string FilePrefix { get; set; } = "";
        public FitDirectionType FitDirection { get; set; } = FitDirectionType.Horizontal;
        public ZoomFitType ZoomType { get; set; } = ZoomFitType.FitToPage;
        public bool ExportShadowViews { get; set; } = true;
        public bool Export3DViews { get; set; } = true;
        public ImageFileType ShadowViewsFileType { get; set; } = ImageFileType.PNG;

        public ImageExportSettings()
        {
        }

        public ImageExportSettings Clone()
        {
            return new ImageExportSettings
            {
                OutputFolder = this.OutputFolder,
                ImageFormat = this.ImageFormat,
                Resolution = this.Resolution,
                UseCustomPixelSize = this.UseCustomPixelSize,
                PixelSize = this.PixelSize,
                ZoomPercentage = this.ZoomPercentage,
                FilePrefix = this.FilePrefix,
                FitDirection = this.FitDirection,
                ZoomType = this.ZoomType,
                ExportShadowViews = this.ExportShadowViews,
                Export3DViews = this.Export3DViews,
                ShadowViewsFileType = this.ShadowViewsFileType
            };
        }

        public string GetFileExtension()
        {
            switch (ImageFormat)
            {
                case ImageFileType.PNG:
                    return "png";
                case ImageFileType.JPEGLossless:
                case ImageFileType.JPEGMedium:
                case ImageFileType.JPEGSmallest:
                    return "jpg";
                case ImageFileType.TIFF:
                    return "tiff";
                case ImageFileType.BMP:
                    return "bmp";
                default:
                    return "png";
            }
        }

        public int GetDPI()
        {
            switch (Resolution)
            {
                case ImageResolution.DPI_72:
                    return 72;
                case ImageResolution.DPI_150:
                    return 150;
                case ImageResolution.DPI_300:
                    return 300;
                case ImageResolution.DPI_600:
                    return 600;
                // DPI_1200 không có trong Revit API 2023
                default:
                    return 600;
            }
        }
    }
}
