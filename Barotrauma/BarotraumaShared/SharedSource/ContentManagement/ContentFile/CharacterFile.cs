﻿using Barotrauma.Extensions;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma
{
    [RequiredByCorePackage]
    sealed class CharacterFile : ContentFile
    {
        public CharacterFile(ContentPackage contentPackage, ContentPath path) : base(contentPackage, path) { }

        public override void LoadFile()
        {
            ClearCaches();
            XDocument doc = XMLExtensions.TryLoadXml(Path);
            if (doc == null)
            {
                DebugConsole.ThrowError($"Loading character file failed: {Path}", contentPackage: ContentPackage);
                return;
            }
            if (CharacterPrefab.Prefabs.AllPrefabs.Any(kvp => kvp.Value.Any(cf => cf?.ContentFile == this)))
            {
                DebugConsole.ThrowError($"Duplicate path: {Path}", contentPackage: ContentPackage);
                return;
            }
            var mainElement = doc.Root.FromPackage(ContentPackage);
            bool isOverride = mainElement.IsOverride();
            if (isOverride) { mainElement = mainElement.FirstElement(); }
            if (!CharacterPrefab.CheckSpeciesName(mainElement, this, out Identifier n)) { return; }
            var prefab = new CharacterPrefab(mainElement, this);
            CharacterPrefab.Prefabs.Add(prefab, isOverride);
        }

        public override void UnloadFile()
        {
            CharacterPrefab.Prefabs.RemoveByFile(this);
            ClearCaches();
        }
        
        private static void ClearCaches()
        {
            // Clear the caches to get rid of any overrides.
            // Variants should have their own params instances, but let's keep it simple and play safe.
            RagdollParams.ClearCache();
            AnimationParams.ClearCache();
        }

        public override void Sort()
        {
            CharacterPrefab.Prefabs.SortAll();
        }

        public override void Preload(Action<Sprite> addPreloadedSprite)
        {
#if CLIENT
            CharacterPrefab characterPrefab = CharacterPrefab.FindByFilePath(Path.Value);
            if (characterPrefab?.ConfigElement == null)
            {
                throw new Exception($"Failed to load the character config file from {Path}!");
            }
            var mainElement = characterPrefab.ConfigElement;
            mainElement.GetChildElements("sound").ForEach(e => RoundSound.Load(e));
            if (!CharacterPrefab.CheckSpeciesName(mainElement, this, out Identifier speciesName)) { return; }
            bool humanoid = mainElement.GetAttributeBool("humanoid", false);
            RagdollParams ragdollParams;
            try
            {
                if (humanoid)
                {
                    ragdollParams = RagdollParams.GetDefaultRagdollParams<HumanRagdollParams>(speciesName, mainElement, ContentPackage);
                }
                else
                {
                    ragdollParams = RagdollParams.GetDefaultRagdollParams<FishRagdollParams>(speciesName, mainElement, ContentPackage);
                }
            }
            catch (Exception e)
            {
                DebugConsole.ThrowError($"Failed to preload a ragdoll file for the character \"{characterPrefab.Name}\"", e,
                    contentPackage: characterPrefab.ContentPackage);
                return;
            }

            if (ragdollParams != null)
            {
                HashSet<ContentPath> texturePaths = new HashSet<ContentPath>();
                AddTexturePath(ragdollParams.Texture);
                foreach (RagdollParams.LimbParams limb in ragdollParams.Limbs)
                {
                    AddTexturePath(limb.normalSpriteParams?.Texture);
                    AddTexturePath(limb.deformSpriteParams?.Texture);
                    AddTexturePath(limb.damagedSpriteParams?.Texture);
                    foreach (var decorativeSprite in limb.decorativeSpriteParams)
                    {
                        AddTexturePath(decorativeSprite.Texture);
                    }
                }
                foreach (ContentPath texturePath in texturePaths)
                {
                    addPreloadedSprite(new Sprite(texturePath.Value, Vector2.Zero));
                }

                void AddTexturePath(string path)
                {
                    if (string.IsNullOrEmpty(path)) { return; }
                    texturePaths.Add(ContentPath.FromRaw(characterPrefab.ContentPackage, ragdollParams.Texture));
                }
            }
#endif
        }
    }
}