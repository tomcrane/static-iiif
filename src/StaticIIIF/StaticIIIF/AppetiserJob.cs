namespace StaticIIIF;

/// <summary>
///  A sketch for what a .NET APpetiser might do
/// </summary>
public class AppetiserJob
{
    /// <summary>
    /// Supplied by the caller. Should be a GUID or similar, a URL-safe id.
    /// Appetiser will use this to create a working/scratch space for the job on its own disk
    /// and then clear up afterwards
    /// </summary>
    public required string Id { get; set; }
    
    /// <summary>
    /// Where the source image lives.
    /// Could be s3://... or file:// or other protocols to be supported.
    /// We assume that Appetiser can read the location (it doesn't need to write to it)
    ///
    /// e.g., s3://source-bucket/blah/image_98.tiff
    /// </summary>
    public required Uri Origin { get; set; }
    
    /// <summary>
    /// If present, Appetiser will produce static generated images
    /// </summary>
    public StaticOutputs? StaticOutputs { get; set; }
    
    /// <summary>
    /// If present, Appetiser will produce tilesource images
    /// </summary>
    public FileOutputs? FileOutputs { get; set; }
}


/// <summary>
/// An info.json and manifest.json will be created if this config is used, even if it's just sizes you want.
/// </summary>
public class StaticOutputs
{
    /// <summary>
    /// Create Jpeg derivatives when (and if) making static outputs.
    /// Defaults to true. 
    /// </summary>
    public bool Jpeg { get; set; } = true;
    
    /// <summary>
    /// Create WebP derivatives when (and if) making static outputs.
    /// Defaults to true. 
    /// </summary>
    public bool WebP { get; set; } = true;
    
    
    /// <summary>
    /// Whether or not to create a static image pyramid of tiles.
    /// </summary>
    public bool MakePyramid { get; set; }

    /// <summary>
    /// if MakePyramid=true, tile size to use.
    /// </summary>
    public int TileSize { get; set; } = 512;
    
    /// <summary>
    /// if MakePyramid=true, whether to save tiles in folders IIIF v2 size format (w,) or v3 format (w,h)
    /// Even if you intend to serve v3 image services tiles you still might want to save the layout in w, ONLY
    /// and support both at the proxy level, to avoid duplicating storage. 
    /// </summary>
    public IIIFLayout LayoutVersion { get; set; } = IIIFLayout.V2;
    
    /// <summary>
    /// Where to save the static outputs
    /// Can be S3, file etc
    /// We assume that Appetiser has write access to this location
    /// An info.json will be created in the root of this location, as well as a single-asset
    /// manifest.json which may be useful for viewers.
    ///
    /// (NB this does not replace the DLCS single asset manifest as it knows nothing about metadata, auth, tags etc)
    /// </summary>
    public required Uri OutputLocation { get; set; }
    
    /// <summary>
    /// If present, Appetiser will save an image to /full/max/0/default.XXX in the output location,
    /// and add the specific size to the `sizes` property and save it as an additional size (unless it's already
    /// present in the sizes list).
    /// </summary>
    public string? Max { get; set; }

    /// <summary>
    /// A list of Image API size parameters.
    /// If there are any entries in this list, Appetiser will produce images at /full/(size)/0/default.XXX
    /// using the configured layout(s) and format(s).
    /// It will also add the actual computed sizes to the info.json, and use the smallest one as a thumbnail
    /// for the canvas in that manifest.
    ///
    /// Examples:
    ///
    /// "!100,100",
    /// "500,"
    /// </summary>
    public List<string> Sizes { get; set; } = [];

    /// <summary>
    /// Used as the `id` (or `@id`) in the generated info.json
    /// </summary>
    public required string ServiceUrl { get; set; }
}


public class FileOutputs
{
    /// <summary>
    /// Where to save the generated file(s)
    /// Can be S3, file etc
    /// We assume that Appetiser has write access to this location
    /// </summary>
    public required Uri OutputLocation { get; set; }
    
    /// <summary>
    /// A list of output formats.
    ///
    /// To max it out you could make a JP2, a jpg-encoded pyramidal tiff, and a webp-encoded pyramidal tiff.
    ///
    /// But usually you'll just have one entry in this list.
    /// </summary>
    public List<ITileFileOutput> Outputs { get; set; } = [];
    
}

public interface ITileFileOutput
{
    TileFileFormat Format { get; }
    
    /// <summary>
    /// Name of file to save. If blank, will generate a name for it
    /// - e.g., {job-id}.webp.tiff for a webp-encoded pyramidal tiff
    /// or {job-id}.jp2 for a JPEG 2000.  
    /// </summary>
    string? FileName { get; set; }
}

public class Jpeg2000Format : ITileFileOutput
{
    public TileFileFormat Format => TileFileFormat.Jpeg2000;
    public string? FileName { get; set; }
    
    /// <summary>
    /// TBC - args that influence the JP2
    /// </summary>
    public string[] Args { get; set; } = [];
}

public class PyramidalTiffFormat : ITileFileOutput
{
    public TileFileFormat Format => TileFileFormat.PyramidalTiff;
    public string? FileName { get; set; }
    
    /// <summary>
    /// Can be jpg or webp
    /// </summary>
    public required string TileFormat { get; set; }
    
    /// <summary>
    /// Quality setting for the tiles
    /// </summary>
    public int Quality { get; set; }

    public int TileSize { get; set; } = 512;
}

public enum IIIFLayout
{
    V2, V3, V2AndV3
}


public enum TileFileFormat
{
    PyramidalTiff,
    Jpeg2000
}