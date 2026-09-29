namespace Sacco_Management_System.Shared
{
    /// <summary>Single place for the product name. Override in appsettings.json: "Brand": { "Name": "...", "Tagline": "..." }</summary>
    public static class Brand
    {
        public static string Name { get; set; } = "Tajiri Sacco";
        public static string Tagline { get; set; } = "Together we build wealth";
    }
}
