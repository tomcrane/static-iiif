using System.Diagnostics;
using IIIF;
using IIIF.ImageApi;
using IIIF.ImageApi.V3;
using IIIF.Presentation;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Annotation;
using IIIF.Presentation.V3.Content;
using IIIF.Presentation.V3.Strings;
using IIIF.Serialisation;
using NetVips;
using Image = NetVips.Image;

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
        if (settings is { JpegTiles: false, WebPTiles: false, JpegPTiff: false, WebPTiff: false })
        {
            Console.WriteLine("No derivatives to make");
            return;
        }
        
        var stopwatch = new Stopwatch();
        var start = DateTime.Now;
        stopwatch.Start();
        using var im = Image.NewFromFile(imageFile);
        Console.WriteLine($"Loaded {imageFile} in {stopwatch.ElapsedMilliseconds}ms");
        stopwatch.Restart();

        
        try
        {
            if (settings.JpegTiles)
            {
                // Save image pyramid
                im.Dzsave(destFolder, 
                    layout:Enums.ForeignDzLayout.Iiif3, 
                    tileSize:settings.TileSize,
                    id:settings.BaseUrl);
                Console.WriteLine($"Saved JPEG tiles in {stopwatch.ElapsedMilliseconds}ms");
                stopwatch.Restart();
            }

            if (settings.WebPTiles)
            {
                // This will overwrite the info.json but that's OK
                im.Dzsave(destFolder, 
                    layout:Enums.ForeignDzLayout.Iiif3, 
                    tileSize:settings.TileSize,
                    id:settings.BaseUrl,
                    suffix: ".webp");
                Console.WriteLine($"Saved WebP tiles in {stopwatch.ElapsedMilliseconds}ms");
                stopwatch.Restart();
            }

            if (settings.JpegPTiff)
            {
                Directory.CreateDirectory(destFolder); // if not created by above
                im.Tiffsave(
                    filename:$"{destFolder}{Path.DirectorySeparatorChar}jpeg-p.tif",
                    tileWidth: settings.TileSize,
                    tileHeight: settings.TileSize,
                    pyramid: true,
                    compression: Enums.ForeignTiffCompression.Jpeg,
                    q: 75);
                Console.WriteLine($"Generated JPEG Pyramidal tiff in {stopwatch.ElapsedMilliseconds}ms");
                stopwatch.Restart();
            }
            
            if (settings.WebPTiff)
            {
                Directory.CreateDirectory(destFolder); // if not created by above
                im.Tiffsave(
                    filename:$"{destFolder}{Path.DirectorySeparatorChar}webp-p.tif",
                    tileWidth: settings.TileSize,
                    tileHeight: settings.TileSize,
                    pyramid: true,
                    compression: Enums.ForeignTiffCompression.Webp,
                    q: 75);
                Console.WriteLine($"Generated WebP Pyramidal tiff in {stopwatch.ElapsedMilliseconds}ms");
            }
        }
        catch (VipsException exception)
        {
            // Catch and log the VipsException,
            // because we may block the evaluation of this image
            Console.WriteLine("\n" + exception.Message);
        }

        ImageService3 imgSvc;
        var infoJsonFile = Path.Combine(destFolder, "info.json");
        var actualSize = new Size(im.Width, im.Height);
        if (settings.JpegTiles || settings.WebPTiles)
        {
            var infoJson = File.ReadAllText(infoJsonFile);
            imgSvc = infoJson.FromJson<ImageService3>();
        }
        else
        {
            // This is a partially populated image service if you are using a TIFF - get the real one from your image server.
            // Its main purpose is to emit the `sizes`.
            imgSvc = new ImageService3
            {
                Id = $"{settings.BaseUrl}/{destFolder.Split(Path.DirectorySeparatorChar)[^1]}",
                Width = im.Width,
                Height = im.Height,
                Profile = "level2"
            };
        }

        var sizes = new List<Size>();
        if (!string.IsNullOrWhiteSpace(settings.Max))
        {
            stopwatch.Restart();
            var sp = SizeParameter.Parse(settings.Max);
            var maxSize = sp.Resize(actualSize);
            sizes.Add(maxSize);
            var maxAsThumb = im.ThumbnailImage(width: maxSize.Width, height: maxSize.Height, size:Enums.Size.Force);
            Console.WriteLine($"Loaded maxAsThumb in {stopwatch.ElapsedMilliseconds}ms");
            stopwatch.Restart();
            var fullMax0Folder = Path.Combine(destFolder, "full", "max", "0");
            Directory.CreateDirectory(fullMax0Folder);
            if (settings.JpegTiles)
            {
                maxAsThumb.Jpegsave(Path.Combine(fullMax0Folder, "default.jpg"));
                Console.WriteLine($"Saved max thumb as Jpeg in {stopwatch.ElapsedMilliseconds}ms");
            }
            if (settings.WebPTiles)
            {
                maxAsThumb.Webpsave(Path.Combine(fullMax0Folder, "default.webp"));
                Console.WriteLine($"Saved max thumb as WebP in {stopwatch.ElapsedMilliseconds}ms");
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
            stopwatch.Restart();
            var sizeIm = im.ThumbnailImage(width: size.Width, height: size.Height, size:Enums.Size.Force);
            var sizeFolder =  Path.Combine(destFolder, "full", $"{size.Width},{size.Height}", "0"); // v3 size
            Directory.CreateDirectory(sizeFolder);
            if (settings.JpegTiles)
            {
                sizeIm.Jpegsave(Path.Combine(sizeFolder, "default.jpg"));
            }
            if (settings.WebPTiles)
            {
                sizeIm.Webpsave(Path.Combine(sizeFolder, "default.webp"));
            }

            if (settings.MakeWidthOnlySize)
            {
                // temporary
                var sizeFolderW =  Path.Combine(destFolder, "full", $"{size.Width},", "0"); // v2 w, size
                Directory.CreateDirectory(sizeFolderW);
                foreach (string img in Directory.GetFiles(sizeFolder, "*.*"))
                {
                    File.Copy(img, img.Replace(sizeFolder, sizeFolderW), true);
                }
            }
            Console.WriteLine($"Created {size} thumb in {stopwatch.ElapsedMilliseconds}ms");
        }

        if (sizes.Any())
        {
            sizes.Sort((a, b) => a.Width.CompareTo(b.Width));
            imgSvc.Sizes = sizes;
        }
        if (settings.WebPTiles)
        {
            imgSvc.PreferredFormats = ["webp"];
            imgSvc.ExtraFormats = ["webp"];
        }
        File.WriteAllText(infoJsonFile, imgSvc.AsJson());
        
        var imageResourceSize = sizes.LastOrDefault() ?? actualSize;
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
                                        Format = "image/jpeg",
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

        if (settings.JpegPTiff)
        {
            manifest.Items[0].Rendering ??= [];
            manifest.Items[0].Rendering.Add(new ExternalResource("Image")
            {
                Id = imgSvc.Id + "/jpeg-p.tif",
                Format = "image/tiff",
                Label = new LanguageMap("en", "Pyramidal tiff encoded with JPEG tiles")
            });
        }
        if (settings.WebPTiff)
        {
            manifest.Items[0].Rendering ??= [];
            manifest.Items[0].Rendering!.Add(new ExternalResource("Image")
            {
                Id = imgSvc.Id + "/webp-p.tif",
                Format = "image/tiff",
                Label = new LanguageMap("en", "Pyramidal tiff encoded with WebP tiles")
            });
        }
        
        manifest.EnsureContext(Context.Presentation3Context);
        
        var manifestFile = infoJsonFile.Replace("info.json", "manifest.json");
        File.WriteAllText(manifestFile, manifest.AsJson());
        
        Console.WriteLine($"Total time taken {(DateTime.Now - start).TotalMilliseconds}ms");
    }
}