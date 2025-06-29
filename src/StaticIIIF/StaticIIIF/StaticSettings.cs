namespace StaticIIIF;

public class StaticSettings
{
    public bool Jpeg { get; set; } = true;
    public bool WebP { get; set; } = true;
    public string Max { get; set; } = "!1600,1600";
    public int TileSize { get; set; } = 512;

    public List<string> Sizes { get; set; } =
    [
        // Any valid IIIF size param!
        "!100,100",
        "!200,200",
        "!400,400",
        "500,", // yeah even that
        "!1000,1000"
        // Max will be added if not present already
    ];

    public string BaseUrl { get; set; } = "https://tomcrane.github.io/yesterdays-lambeth-today/iiif-img";
}