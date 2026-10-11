using UnityEditor;

namespace GodGame.Editor
{
    /// <summary>
    /// Import settings for exported art, enforced per folder so nobody sets them by hand (plan, section 7.6).
    /// Everything under Art/Generated comes from tools/blender/export.py.
    /// </summary>
    public sealed class ArtImportPostprocessor : AssetPostprocessor
    {
        public const string GeneratedFolder = "Assets/_Game/Art/Generated/";

        public override uint GetVersion() => 1;

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(GeneratedFolder))
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            bool isAnimations = assetPath.EndsWith("_Anims.fbx");

            // Blender is Z-up; the exporter writes Y-up axes and Unity bakes the conversion,
            // so objects import unrotated at scale 1.
            importer.bakeAxisConversion = true;
            importer.useFileScale = true;
            importer.globalScale = 1f;

            // Flat palette colours come from one shared material, assigned by the renderer.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importBlendShapes = false;
            importer.addCollider = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;

            // The VAT baker samples meshes and clips on the CPU.
            importer.isReadable = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;

            // Soldiers play baked vertex animation, not Mecanim: a generic rig, clips only in the _Anims file.
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = isAnimations;
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
        }
    }
}
