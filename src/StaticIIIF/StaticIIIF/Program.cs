using IIIF;
using IIIF.ImageApi;
using IIIF.ImageApi.V2;
using IIIF.ImageApi.V3;
using IIIF.Presentation;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using NetVips;

namespace StaticIIIF;

class Program
{
    static void Main(string[] args)
    {
        if (ModuleInitializer.VipsInitialized)
        {
            Console.WriteLine($"Inited libvips {NetVips.NetVips.Version(0)}.{NetVips.NetVips.Version(1)}.{NetVips.NetVips.Version(2)}");
        }
        else
        {
            Console.WriteLine(ModuleInitializer.Exception.Message);
            return;
        }

        if (args.Length != 2)
        {
            Console.WriteLine("Args should be source-image, dest-folder");
            return;
        }
        
        var imageFile = args[0];
        var destFolder = args[1];
        
        Process(imageFile, destFolder);
    }

    private static void Process(string imageFile, string destFolder)
    {
        var settings = new StaticSettings();
        using var im = Image.NewFromFile(imageFile);
        
        try
        {
            if (settings.Jpeg)
            {
                // Save image pyramid
                im.Dzsave(destFolder, 
                    layout:Enums.ForeignDzLayout.Iiif3, 
                    tileSize:settings.TileSize,
                    id:settings.BaseUrl);
            }

            if (settings.WebP)
            {
                // This will overwrite the info.json but that's OK
                im.Dzsave(destFolder, 
                    layout:Enums.ForeignDzLayout.Iiif3, 
                    tileSize:settings.TileSize,
                    id:settings.BaseUrl,
                    suffix: ".webp");
            }
        }
        catch (VipsException exception)
        {
            // Catch and log the VipsException,
            // because we may block the evaluation of this image
            Console.WriteLine("\n" + exception.Message);
        }

        var infoJsonFile = Path.Combine(destFolder, "info.json");
        var infoJson = File.ReadAllText(infoJsonFile);
        var imgSvc = infoJson.FromJson<ImageService3>();
        var actualSize = new Size(imgSvc.Width, imgSvc.Height);

        var sizes = new List<Size>();
        if (!string.IsNullOrWhiteSpace(settings.Max))
        {
            var sp = SizeParameter.Parse(settings.Max);
            var maxSize = sp.Resize(actualSize);
            sizes.Add(maxSize);
            var maxAsThumb = im.ThumbnailImage(width: maxSize.Width, height: maxSize.Height, size:Enums.Size.Force);
            var fullMax0Folder = Path.Combine(destFolder, "full", "max", "0");
            Directory.CreateDirectory(fullMax0Folder);
            if (settings.Jpeg)
            {
                maxAsThumb.Jpegsave(Path.Combine(fullMax0Folder, "default.jpg"));
            }
            if (settings.WebP)
            {
                maxAsThumb.Webpsave(Path.Combine(fullMax0Folder, "default.webp"));
            }
        }

        foreach (var sizeParam in settings.Sizes)
        {
            var sp = SizeParameter.Parse(sizeParam);
            var targetSize = sp.Resize(actualSize);
            if (sizes.All(s => s.Width != targetSize.Width))
            {
                sizes.Add(targetSize);
            }
        }
        
        foreach (var size in sizes)
        {
            var sizeIm = im.ThumbnailImage(width: size.Width, height: size.Height, size:Enums.Size.Force);
            var sizeFolder =  Path.Combine(destFolder, "full", $"{size.Width},{size.Height}", "0"); // v3 size
            Directory.CreateDirectory(sizeFolder);
            if (settings.Jpeg)
            {
                sizeIm.Jpegsave(Path.Combine(sizeFolder, "default.jpg"));
            }
            if (settings.WebP)
            {
                sizeIm.Webpsave(Path.Combine(sizeFolder, "default.webp"));
            }
        }

        if (sizes.Any())
        {
            sizes.Sort((a, b) => a.Width.CompareTo(b.Width));
            imgSvc.Sizes = sizes;
        }
        if (settings.WebP)
        {
            imgSvc.PreferredFormats = ["webp"];
            imgSvc.ExtraFormats = ["webp"];
        }
        
        File.WriteAllText(infoJsonFile, imgSvc.AsJson());
        if (sizes.Count == 0)
        {
            return;
        }

        var imageResourceSize = sizes.Last();
        var manifest = new Manifest
        {
            Id = imgSvc.Id + "/manifest.json",
            Label = new LanguageMap("en", imageFile),
            Items =
            [
                new Canvas
                {
                    Id = imgSvc.Id + "/canvas",
                    Label = new LanguageMap("en", "single canvas for " + imageFile),
                    Width = actualSize.Width,
                    Height = actualSize.Height,
                    Items = [
                        new AnnotationPage
                        {
                            Id = imgSvc.Id + "/page",
                            Label = new LanguageMap("en", "single anno page for " + imageFile),
                            Items = [
                                new PaintingAnnotation
                                {
                                    Id = imgSvc.Id + "/painting",
                                    Body = new IIIF.Presentation.V3.Content.Image
                                    {
                                        Id = $"{imgSvc.Id}/full/{imageResourceSize.Width},{imageResourceSize.Height}/0/default.jpg",
                                        Width = imageResourceSize.Width,
                                        Height = imageResourceSize.Height,
                                        Format = "image/jpg",
                                        Service = [ imgSvc ]
                                    },
                                    Target = new Canvas
                                    {
                                        Id = imgSvc.Id + "/canvas"
                                    }
                                }
                            ]
                        }
                    ]
                }
            ]
        };
        
        manifest.EnsureContext(Context.Presentation3Context);
        
        var manifestFile = infoJsonFile.Replace("info.json", "manifest.json");
        File.WriteAllText(manifestFile, manifest.AsJson());
    }
}