namespace Poi
{
    /// <summary>
    /// Builds the data objects the scene and the tests share. The demo does not new them inline.
    /// </summary>
    public sealed class PoiComposition
    {
        public PoiService Service { get; }
        public PoiFeedSimulator Simulator { get; }
        public EdgeIndicatorQuery Edges { get; }

        PoiComposition(PoiService service, PoiFeedSimulator simulator, EdgeIndicatorQuery edges)
        {
            Service = service;
            Simulator = simulator;
            Edges = edges;
        }

        public static PoiComposition Create(float worldSize, float cellSize, int seed, PoiSeedCatalog catalog)
        {
            var service = new PoiService(worldSize, cellSize);
            var simulator = new PoiFeedSimulator(service, seed);
            var edges = new EdgeIndicatorQuery(service.Store, service.Index);
            if (catalog != null && catalog.entries != null)
            {
                service.LoadSeed(catalog.entries);
            }

            return new PoiComposition(service, simulator, edges);
        }
    }
}
