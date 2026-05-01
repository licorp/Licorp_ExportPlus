using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace LicorpExportPlus.Services
{
    public class ImageExportService
    {
        public void ExportToImages(List<ViewSheet> sheets, ImageExportSettings settings)
        {
            var imageOptions = new ImageExportOptions
            {
                ZoomType = ZoomFitType.FitToPage,
                ImageResolution = GetImageResolution(settings.Resolution),
                ExportRange = ExportRange.SetOfViews,
                HLRandWFViewsFileType = settings.ImageFormat,
                ShadowViewsFileType = settings.ImageFormat
            };

            foreach (var sheet in sheets)
            {
                var viewIds = new List<ElementId> { sheet.Id };
                imageOptions.SetViewsAndSheets(viewIds);

                try
                {
                    sheet.Document.ExportImage(imageOptions);
                }
                catch (Exception ex)
                {
                }
            }
        }

        private ImageResolution GetImageResolution(int dpi)
        {
            switch (dpi)
            {
                case 72: return ImageResolution.DPI_72;
                case 150: return ImageResolution.DPI_150;
                case 300: return ImageResolution.DPI_300;
                case 600: return ImageResolution.DPI_600;
                default: return ImageResolution.DPI_300;
            }
        }
    }

    public class ImageExportSettings
    {
        public string OutputFolder { get; set; }
        public int Resolution { get; set; } = 300;
        public ImageFileType ImageFormat { get; set; } = ImageFileType.PNG;
        public bool UseCustomPixelSize { get; set; }
        public int PixelSize { get; set; }
        public int ZoomPercentage { get; set; }
    }
}
