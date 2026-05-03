using Dapper;
using Npgsql;
using MyDashboardApi.Models;

namespace MyDashboardApi.Database.Repositories;

public class ProductRepository(NpgsqlDataSource dataSource) : IProductRepository
{
    public async Task<IEnumerable<ProductListItem>> GetProductsAsync()
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        return await conn.QueryAsync<ProductListItem>("""
            SELECT id, number, sku, description, code
            FROM products
            ORDER BY number
            """);
    }

    public async Task<ProductDetail?> GetProductDetailAsync(int id)
    {
        await using var conn = await dataSource.OpenConnectionAsync();

        var product = await conn.QueryFirstOrDefaultAsync<ProductDetail>("""
            SELECT id, number, description, sku, code,
                   package_code, initial_code, instruction,
                   length, width, thickness, density
            FROM products WHERE id = @id
            """, new { id });

        if (product is null) return null;

        product.GeneralSp     = await conn.QueryFirstOrDefaultAsync<GeneralSp>(
            "SELECT * FROM general_sp    WHERE product_id = @id", new { id });
        product.SawsSp        = await conn.QueryFirstOrDefaultAsync<SawsSp>(
            "SELECT * FROM saws_sp       WHERE product_id = @id", new { id });
        product.TahuSp        = await conn.QueryFirstOrDefaultAsync<TahuSp>(
            "SELECT * FROM tahu_sp       WHERE product_id = @id", new { id });
        product.BundlerSp     = await conn.QueryFirstOrDefaultAsync<BundlerSp>(
            "SELECT * FROM bundler_sp    WHERE product_id = @id", new { id });
        product.ConsumablesSp = await conn.QueryFirstOrDefaultAsync<ConsumablesSp>(
            "SELECT * FROM consumables_sp WHERE product_id = @id", new { id });
        product.UlSp          = await conn.QueryFirstOrDefaultAsync<UlSp>(
            "SELECT * FROM ul_sp         WHERE product_id = @id", new { id });

        return product;
    }

    public async Task<bool> UpdateProductAsync(int id, UpdateProductRequest req, int userId)
    {
        await using var conn = await dataSource.OpenConnectionAsync();

        var rows = await conn.ExecuteAsync("""
            UPDATE products SET
                package_code = @PackageCode,
                initial_code = @InitialCode,
                instruction  = @Instruction,
                length       = @Length,
                width        = @Width,
                thickness    = @Thickness,
                density      = @Density,
                modified_at  = NOW(),
                modified_by  = @userId
            WHERE id = @id
            """, new { id, userId, req.PackageCode, req.InitialCode, req.Instruction,
                       req.Length, req.Width, req.Thickness, req.Density });

        if (rows == 0) return false;

        if (req.GeneralSp is { } g)
            await conn.ExecuteAsync("""
                INSERT INTO general_sp (product_id,
                    package, abc_cat, waste_suply, remark, info, labelling, state,
                    data_check, drum_pressure, saw_cross, labelling_state, product_type,
                    split_in_pair_113_114, product_turn_pos_122,
                    weight_limit_max_perc, weight_limit_min_perc,
                    flexi_turn, flexi_width, energy_class, binder_type, pkf_group,
                    modified_at, modified_by)
                VALUES (@id,
                    @Package, @AbcCat, @WasteSuply, @Remark, @Info, @Labelling, @State,
                    @DataCheck, @DrumPressure, @SawCross, @LabellingState, @ProductType,
                    @SplitInPair113114, @ProductTurnPos122,
                    @WeightLimitMaxPerc, @WeightLimitMinPerc,
                    @FlexiTurn, @FlexiWidth, @EnergyClass, @BinderType, @PkfGroup,
                    NOW(), @userId)
                ON CONFLICT (product_id) DO UPDATE SET
                    package               = EXCLUDED.package,
                    abc_cat               = EXCLUDED.abc_cat,
                    waste_suply           = EXCLUDED.waste_suply,
                    remark                = EXCLUDED.remark,
                    info                  = EXCLUDED.info,
                    labelling             = EXCLUDED.labelling,
                    state                 = EXCLUDED.state,
                    data_check            = EXCLUDED.data_check,
                    drum_pressure         = EXCLUDED.drum_pressure,
                    saw_cross             = EXCLUDED.saw_cross,
                    labelling_state       = EXCLUDED.labelling_state,
                    product_type          = EXCLUDED.product_type,
                    split_in_pair_113_114 = EXCLUDED.split_in_pair_113_114,
                    product_turn_pos_122  = EXCLUDED.product_turn_pos_122,
                    weight_limit_max_perc = EXCLUDED.weight_limit_max_perc,
                    weight_limit_min_perc = EXCLUDED.weight_limit_min_perc,
                    flexi_turn            = EXCLUDED.flexi_turn,
                    flexi_width           = EXCLUDED.flexi_width,
                    energy_class          = EXCLUDED.energy_class,
                    binder_type           = EXCLUDED.binder_type,
                    pkf_group             = EXCLUDED.pkf_group,
                    modified_at           = EXCLUDED.modified_at,
                    modified_by           = EXCLUDED.modified_by
                """, new { id, userId, g.Package, g.AbcCat, g.WasteSuply, g.Remark, g.Info,
                           g.Labelling, g.State, g.DataCheck, g.DrumPressure, g.SawCross,
                           g.LabellingState, g.ProductType, g.SplitInPair113114,
                           g.ProductTurnPos122, g.WeightLimitMaxPerc, g.WeightLimitMinPerc,
                           g.FlexiTurn, g.FlexiWidth, g.EnergyClass, g.BinderType, g.PkfGroup });

        if (req.SawsSp is { } s)
            await conn.ExecuteAsync("""
                INSERT INTO saws_sp (product_id,
                    trimming_waste_ows, plates_in_pkg, cut_direction, layers,
                    waste_std, trimming_waste_ow, sheet_width, cut_width, raw_edge_width,
                    modified_at, modified_by)
                VALUES (@id,
                    @TrimmingWasteOws, @PlatesInPkg, @CutDirection, @Layers,
                    @WasteStd, @TrimmingWasteOw, @SheetWidth, @CutWidth, @RawEdgeWidth,
                    NOW(), @userId)
                ON CONFLICT (product_id) DO UPDATE SET
                    trimming_waste_ows = EXCLUDED.trimming_waste_ows,
                    plates_in_pkg      = EXCLUDED.plates_in_pkg,
                    cut_direction      = EXCLUDED.cut_direction,
                    layers             = EXCLUDED.layers,
                    waste_std          = EXCLUDED.waste_std,
                    trimming_waste_ow  = EXCLUDED.trimming_waste_ow,
                    sheet_width        = EXCLUDED.sheet_width,
                    cut_width          = EXCLUDED.cut_width,
                    raw_edge_width     = EXCLUDED.raw_edge_width,
                    modified_at        = EXCLUDED.modified_at,
                    modified_by        = EXCLUDED.modified_by
                """, new { id, userId, s.TrimmingWasteOws, s.PlatesInPkg, s.CutDirection,
                           s.Layers, s.WasteStd, s.TrimmingWasteOw, s.SheetWidth,
                           s.CutWidth, s.RawEdgeWidth });

        if (req.TahuSp is { } t)
            await conn.ExecuteAsync("""
                INSERT INTO tahu_sp (product_id,
                    tahu_finish_pack_height, tahu_output_height, tahu_side_welding,
                    tahu_film_width, tahu_vacuum, tahu_use_shrink_heat, tahu_smart_date,
                    tahu_foil_code, modified_at, modified_by)
                VALUES (@id,
                    @TahuFinishPackHeight, @TahuOutputHeight, @TahuSideWelding,
                    @TahuFilmWidth, @TahuVacuum, @TahuUseShrinkHeat, @TahuSmartDate,
                    @TahuFoilCode, NOW(), @userId)
                ON CONFLICT (product_id) DO UPDATE SET
                    tahu_finish_pack_height = EXCLUDED.tahu_finish_pack_height,
                    tahu_output_height      = EXCLUDED.tahu_output_height,
                    tahu_side_welding       = EXCLUDED.tahu_side_welding,
                    tahu_film_width         = EXCLUDED.tahu_film_width,
                    tahu_vacuum             = EXCLUDED.tahu_vacuum,
                    tahu_use_shrink_heat    = EXCLUDED.tahu_use_shrink_heat,
                    tahu_smart_date         = EXCLUDED.tahu_smart_date,
                    tahu_foil_code          = EXCLUDED.tahu_foil_code,
                    modified_at             = EXCLUDED.modified_at,
                    modified_by             = EXCLUDED.modified_by
                """, new { id, userId, t.TahuFinishPackHeight, t.TahuOutputHeight,
                           t.TahuSideWelding, t.TahuFilmWidth, t.TahuVacuum,
                           t.TahuUseShrinkHeat, t.TahuSmartDate, t.TahuFoilCode });

        if (req.BundlerSp is { } b)
            await conn.ExecuteAsync("""
                INSERT INTO bundler_sp (product_id,
                    bundler_packs_per_bundle, bundler_comp_length, bundler_output_length,
                    product_turn_pos_608, group_product_pos_608, modified_at, modified_by)
                VALUES (@id,
                    @BundlerPacksPerBundle, @BundlerCompLength, @BundlerOutputLength,
                    @ProductTurnPos608, @GroupProductPos608, NOW(), @userId)
                ON CONFLICT (product_id) DO UPDATE SET
                    bundler_packs_per_bundle = EXCLUDED.bundler_packs_per_bundle,
                    bundler_comp_length      = EXCLUDED.bundler_comp_length,
                    bundler_output_length    = EXCLUDED.bundler_output_length,
                    product_turn_pos_608     = EXCLUDED.product_turn_pos_608,
                    group_product_pos_608    = EXCLUDED.group_product_pos_608,
                    modified_at              = EXCLUDED.modified_at,
                    modified_by              = EXCLUDED.modified_by
                """, new { id, userId, b.BundlerPacksPerBundle, b.BundlerCompLength,
                           b.BundlerOutputLength, b.ProductTurnPos608, b.GroupProductPos608 });

        if (req.ConsumablesSp is { } c)
            await conn.ExecuteAsync("""
                INSERT INTO consumables_sp (product_id,
                    bundle_plastic_code, hooder_plastic_code, wrapper_plastic_code,
                    check_layers, modified_at, modified_by)
                VALUES (@id,
                    @BundlePlasticCode, @HooderPlasticCode, @WrapperPlasticCode,
                    @CheckLayers, NOW(), @userId)
                ON CONFLICT (product_id) DO UPDATE SET
                    bundle_plastic_code  = EXCLUDED.bundle_plastic_code,
                    hooder_plastic_code  = EXCLUDED.hooder_plastic_code,
                    wrapper_plastic_code = EXCLUDED.wrapper_plastic_code,
                    check_layers         = EXCLUDED.check_layers,
                    modified_at          = EXCLUDED.modified_at,
                    modified_by          = EXCLUDED.modified_by
                """, new { id, userId, c.BundlePlasticCode, c.HooderPlasticCode,
                           c.WrapperPlasticCode, c.CheckLayers });

        if (req.UlSp is { } u)
            await conn.ExecuteAsync("""
                INSERT INTO ul_sp (product_id,
                    ul_product_per_layer, ul_pallet_layers, ul_layers_interlocked,
                    ul_pack_orientation, ul_direction_base_layer, ul_miwo_feet,
                    ul_miwo_dim, ul_pallet_dim, ul_pallet_dim_perpendicular,
                    ul_pallet_height, ul_cross_turning, ul_use_hooding,
                    ul_use_glue, ul_use_wrapping, modified_at, modified_by)
                VALUES (@id,
                    @UlProductPerLayer, @UlPalletLayers, @UlLayersInterlocked,
                    @UlPackOrientation, @UlDirectionBaseLayer, @UlMiwoFeet,
                    @UlMiwoDim, @UlPalletDim, @UlPalletDimPerpendicular,
                    @UlPalletHeight, @UlCrossTurning, @UlUseHooding,
                    @UlUseGlue, @UlUseWrapping, NOW(), @userId)
                ON CONFLICT (product_id) DO UPDATE SET
                    ul_product_per_layer        = EXCLUDED.ul_product_per_layer,
                    ul_pallet_layers            = EXCLUDED.ul_pallet_layers,
                    ul_layers_interlocked       = EXCLUDED.ul_layers_interlocked,
                    ul_pack_orientation         = EXCLUDED.ul_pack_orientation,
                    ul_direction_base_layer     = EXCLUDED.ul_direction_base_layer,
                    ul_miwo_feet                = EXCLUDED.ul_miwo_feet,
                    ul_miwo_dim                 = EXCLUDED.ul_miwo_dim,
                    ul_pallet_dim               = EXCLUDED.ul_pallet_dim,
                    ul_pallet_dim_perpendicular = EXCLUDED.ul_pallet_dim_perpendicular,
                    ul_pallet_height            = EXCLUDED.ul_pallet_height,
                    ul_cross_turning            = EXCLUDED.ul_cross_turning,
                    ul_use_hooding              = EXCLUDED.ul_use_hooding,
                    ul_use_glue                 = EXCLUDED.ul_use_glue,
                    ul_use_wrapping             = EXCLUDED.ul_use_wrapping,
                    modified_at                 = EXCLUDED.modified_at,
                    modified_by                 = EXCLUDED.modified_by
                """, new { id, userId, u.UlProductPerLayer, u.UlPalletLayers,
                           u.UlLayersInterlocked, u.UlPackOrientation, u.UlDirectionBaseLayer,
                           u.UlMiwoFeet, u.UlMiwoDim, u.UlPalletDim,
                           u.UlPalletDimPerpendicular, u.UlPalletHeight, u.UlCrossTurning,
                           u.UlUseHooding, u.UlUseGlue, u.UlUseWrapping });

        return true;
    }
}
