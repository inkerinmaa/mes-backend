namespace MyDashboardApi.Models;

public record ProductListItem(int Id, string Number, string? Sku, string? Description, string? Code);

public class ProductDetail
{
    public int     Id          { get; set; }
    public string  Number      { get; set; } = "";
    public string? Description { get; set; }
    public string? Sku         { get; set; }
    public string? Code        { get; set; }
    public string? PackageCode { get; set; }
    public string? InitialCode { get; set; }
    public string? Instruction { get; set; }
    public decimal? Length     { get; set; }
    public decimal? Width      { get; set; }
    public decimal? Thickness  { get; set; }
    public decimal? Density    { get; set; }
    public GeneralSp?      GeneralSp      { get; set; }
    public SawsSp?         SawsSp         { get; set; }
    public TahuSp?         TahuSp         { get; set; }
    public BundlerSp?      BundlerSp      { get; set; }
    public ConsumablesSp?  ConsumablesSp  { get; set; }
    public UlSp?           UlSp           { get; set; }
}

public class GeneralSp
{
    public string?  Package              { get; set; }
    public string?  AbcCat               { get; set; }
    public decimal? WasteSuply           { get; set; }
    public string?  Remark               { get; set; }
    public string?  Info                 { get; set; }
    public string?  Labelling            { get; set; }
    public string?  State                { get; set; }
    public bool?    DataCheck            { get; set; }
    public decimal? DrumPressure         { get; set; }
    public decimal? SawCross             { get; set; }
    public string?  LabellingState       { get; set; }
    public string?  ProductType          { get; set; }
    public bool?    SplitInPair113114    { get; set; }
    public string?  ProductTurnPos122    { get; set; }
    public decimal? WeightLimitMaxPerc   { get; set; }
    public decimal? WeightLimitMinPerc   { get; set; }
    public bool?    FlexiTurn            { get; set; }
    public decimal? FlexiWidth           { get; set; }
    public string?  EnergyClass          { get; set; }
    public string?  BinderType           { get; set; }
    public string?  PkfGroup             { get; set; }
}

public class SawsSp
{
    public decimal? TrimmingWasteOws { get; set; }
    public int?     PlatesInPkg      { get; set; }
    public string?  CutDirection     { get; set; }
    public int?     Layers           { get; set; }
    public decimal? WasteStd         { get; set; }
    public decimal? TrimmingWasteOw  { get; set; }
    public decimal? SheetWidth       { get; set; }
    public decimal? CutWidth         { get; set; }
    public decimal? RawEdgeWidth     { get; set; }
}

public class TahuSp
{
    public decimal? TahuFinishPackHeight { get; set; }
    public decimal? TahuOutputHeight     { get; set; }
    public decimal? TahuSideWelding      { get; set; }
    public decimal? TahuFilmWidth        { get; set; }
    public decimal? TahuVacuum           { get; set; }
    public bool?    TahuUseShrinkHeat    { get; set; }
    public bool?    TahuSmartDate        { get; set; }
    public string?  TahuFoilCode         { get; set; }
}

public class BundlerSp
{
    public int?     BundlerPacksPerBundle { get; set; }
    public decimal? BundlerCompLength     { get; set; }
    public decimal? BundlerOutputLength   { get; set; }
    public string?  ProductTurnPos608     { get; set; }
    public string?  GroupProductPos608    { get; set; }
}

public class ConsumablesSp
{
    public string? BundlePlasticCode  { get; set; }
    public string? HooderPlasticCode  { get; set; }
    public string? WrapperPlasticCode { get; set; }
    public int?    CheckLayers        { get; set; }
}

public class UlSp
{
    public int?     UlProductPerLayer       { get; set; }
    public int?     UlPalletLayers          { get; set; }
    public bool?    UlLayersInterlocked      { get; set; }
    public string?  UlPackOrientation       { get; set; }
    public string?  UlDirectionBaseLayer    { get; set; }
    public int?     UlMiwoFeet              { get; set; }
    public string?  UlMiwoDim               { get; set; }
    public string?  UlPalletDim             { get; set; }
    public string?  UlPalletDimPerpendicular { get; set; }
    public decimal? UlPalletHeight          { get; set; }
    public bool?    UlCrossTurning          { get; set; }
    public bool?    UlUseHooding            { get; set; }
    public bool?    UlUseGlue               { get; set; }
    public bool?    UlUseWrapping           { get; set; }
}

// ── Update request ──────────────────────────────────────────────────────────

public class UpdateProductRequest
{
    public string?  PackageCode   { get; set; }
    public string?  InitialCode   { get; set; }
    public string?  Instruction   { get; set; }
    public decimal? Length        { get; set; }
    public decimal? Width         { get; set; }
    public decimal? Thickness     { get; set; }
    public decimal? Density       { get; set; }
    public GeneralSp?     GeneralSp     { get; set; }
    public SawsSp?        SawsSp        { get; set; }
    public TahuSp?        TahuSp        { get; set; }
    public BundlerSp?     BundlerSp     { get; set; }
    public ConsumablesSp? ConsumablesSp { get; set; }
    public UlSp?          UlSp          { get; set; }
}
