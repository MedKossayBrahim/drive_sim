"""Génère sept variantes GLB allégées ; ne lancer qu'après accord de l'utilisateur.

Depuis n'importe quel dossier, une fois Blender installé :
  /Applications/Blender.app/Contents/MacOS/Blender --background \
    --python-exit-code 1 --python /chemin/drive_sim/docs/blender_decimate.py

Options après « -- » : --repo /chemin/drive_sim --merge-distance 0.00001.
Sortie : Assets/_Plane/Models/<dossier>/<nom>_lod0.glb.
Les modèles originaux, prefabs et réglages Unity ne sont jamais écrits.
Une sortie déjà existante provoque un arrêt avant tout traitement.

Références API :
https://docs.blender.org/api/current/bmesh.ops.html#bmesh.ops.remove_doubles
https://docs.blender.org/api/current/bpy.types.DecimateModifier.html
https://docs.blender.org/api/current/bpy.ops.export_scene.html
"""

import argparse
import hashlib
import json
import math
from pathlib import Path
import shutil
import struct
import sys
import tempfile
import unicodedata

import bmesh
import bpy


# Dossier, nom ASCII, nom dans l'inventaire, cible, ratio indicatif, texture maximale.
# Les ratios arrondis demandés sont conservés pour comparaison ; la cible prévaut.
TASKS = (
    ("Environment", "pine_tree", "sapin.glb", 800, 0.02, 512),
    ("Environment", "rocks", "rochers.glb", 600, 0.03, 512),
    ("Props", "crate", "colis.glb", 500, 0.03, 512),
    ("Airport", "hangar", "hangar.glb", 3000, 0.09, 1024),
    ("Airport", "control_tower", "tour.glb", 3000, 0.17, 1024),
    ("Props", "drop_target", "cible.glb", 1000, 0.09, 1024),
    ("Environment", "wind_turbine", "L'éolienne.glb", 1000, 0.25, 1024),
)


def canonical(name):
    """Accepte les apostrophes typographiques et les accents Unicode décomposés."""
    return unicodedata.normalize("NFC", name).replace("’", "'")


def inventory_counts(path):
    """Lit les triangles de la table de l'inventaire, pas les nombres de meshes."""
    counts = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        if not line.startswith("|"):
            continue
        cells = [cell.strip() for cell in line.strip("|").split("|")]
        if len(cells) < 4:
            continue
        name = cells[0].strip("`")
        value = "".join(cells[3].split())
        if name.endswith(".glb") and value.isdecimal():
            counts[canonical(name)] = int(value)
    return counts


def sha256(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest() if hasattr(hashlib, "file_digest") else hash_stream(stream)


def hash_stream(stream):
    digest = hashlib.sha256()
    for block in iter(lambda: stream.read(1024 * 1024), b""):
        digest.update(block)
    return digest.hexdigest()


def triangle_count(objects):
    """Compte les triangles réels, même si le mesh contient des quads ou des n-gones."""
    total = 0
    for obj in objects:
        obj.data.calc_loop_triangles()
        total += len(obj.data.loop_triangles)
    return total


def merge_vertices(obj, distance):
    # Une copie évite de traiter deux fois un mesh partagé entre plusieurs objets.
    obj.data = obj.data.copy()
    mesh = bmesh.new()
    try:
        mesh.from_mesh(obj.data)
        bmesh.ops.remove_doubles(mesh, verts=list(mesh.verts), dist=distance)
        mesh.to_mesh(obj.data)
        obj.data.update()
    finally:
        mesh.free()


def material_images(objects):
    """Retrouve les images utilisées, y compris dans les groupes de nodes."""
    images, visited = set(), set()

    def visit(tree):
        if tree is None or tree.as_pointer() in visited:
            return
        visited.add(tree.as_pointer())
        for node in tree.nodes:
            if node.type == "TEX_IMAGE" and node.image is not None:
                images.add(node.image)
            if node.type == "GROUP":
                visit(node.node_tree)

    for obj in objects:
        for material in obj.data.materials:
            if material is not None and material.use_nodes:
                visit(material.node_tree)
    return images


def resize_images(objects, limit):
    images = material_images(objects)
    if not images:
        raise RuntimeError("Aucune texture de matériau retrouvée dans ce GLB texturé.")
    for image in images:
        width, height = image.size[:]
        if width <= 0 or height <= 0:
            raise RuntimeError(f"Image non chargée : {image.name}")
        factor = min(1.0, limit / max(width, height))
        size = (max(1, round(width * factor)), max(1, round(height * factor)))
        if size != (width, height):
            image.scale(*size)
        # Reconstitue les données embarquées après scale ; aucune écriture du PNG source.
        image.file_format = "PNG"
        image.pack()
        print(f"  Texture {image.name} : {width}×{height} -> {size[0]}×{size[1]}", flush=True)


def exported_triangles(path):
    """Vérifie le GLB produit et compte ses triangles, avec les instances de la scène."""
    with path.open("rb") as stream:
        magic, version, length = struct.unpack("<4sII", stream.read(12))
        if magic != b"glTF" or version != 2 or length != path.stat().st_size:
            raise RuntimeError("Export GLB invalide.")
        document = None
        while stream.tell() < length:
            size, kind = struct.unpack("<II", stream.read(8))
            if stream.tell() + size > length:
                raise RuntimeError("Chunk GLB tronqué.")
            if kind == 0x4E4F534A:
                document = json.loads(stream.read(size))
            else:
                stream.seek(size, 1)
    if document is None:
        raise RuntimeError("Export sans chunk JSON.")
    meshes = []
    for mesh in document.get("meshes", []):
        triangles = 0
        for primitive in mesh.get("primitives", []):
            accessor = primitive.get("indices", primitive["attributes"]["POSITION"])
            count = document["accessors"][accessor]["count"]
            mode = primitive.get("mode", 4)
            triangles += count // 3 if mode == 4 else max(0, count - 2) if mode in (5, 6) else 0
        meshes.append(triangles)

    def visit(index):
        node = document["nodes"][index]
        return (meshes[node["mesh"]] if "mesh" in node else 0) + sum(visit(i) for i in node.get("children", []))

    scene = document["scenes"][document.get("scene", 0)]
    return sum(visit(index) for index in scene.get("nodes", []))


def process(source, destination, target, expected, nominal_ratio, texture_limit, distance):
    # Réinitialisation en mémoire seulement ; aucun .blend ni fichier de préférences enregistré.
    bpy.ops.wm.read_factory_settings(use_empty=True)
    result = bpy.ops.import_scene.gltf(filepath=str(source))
    if "FINISHED" not in result:
        raise RuntimeError(f"Import échoué : {source}")
    objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    before = triangle_count(objects)
    if before <= 0:
        raise RuntimeError(f"Mesh vide : {source}")
    print(f"\n{source.name} : avant={before}, inventaire={expected}, cible≈{target}", flush=True)
    print(f"  Ratio inventaire={target / expected:.8f} ; indicatif demandé={nominal_ratio:.2f}", flush=True)
    if before != expected:
        print("  ATTENTION : source différente de l'inventaire ; la géométrie importée fait foi.", flush=True)
    for obj in objects:
        merge_vertices(obj, distance)
    cleaned = triangle_count(objects)
    if cleaned == 0:
        raise RuntimeError("Merge by Distance a supprimé toute la géométrie.")
    # Recalcul après nettoyage : le nombre réel de triangles peut avoir changé.
    ratio = min(1.0, target / cleaned)
    print(f"  Après Merge by Distance={cleaned} ; ratio Decimate appliqué={ratio:.8f}", flush=True)
    for obj in objects:
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        modifier = obj.modifiers.new(name="SkyCourier_Decimate_LOD0", type="DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = ratio
        modifier.use_collapse_triangulate = True
        if "FINISHED" not in bpy.ops.object.modifier_apply(modifier=modifier.name):
            raise RuntimeError(f"Decimate échoué : {obj.name}")
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
        obj.data.update()
    after = triangle_count(objects)
    if after <= 0:
        raise RuntimeError("Decimate a supprimé toute la géométrie.")
    resize_images(objects, texture_limit)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.context.scene.objects:
        if obj.type in {"MESH", "EMPTY", "ARMATURE"}:
            obj.select_set(True)
    # Export temporaire : une interruption de Blender ne crée pas un LOD final tronqué.
    with tempfile.TemporaryDirectory(prefix=".sky_courier_lod_", dir=destination.parent) as temporary:
        export = Path(temporary) / destination.name
        result = bpy.ops.export_scene.gltf(
            filepath=str(export), export_format="GLB", use_selection=True,
            export_yup=True, export_materials="EXPORT", export_image_format="AUTO",
            export_keep_originals=False, export_normals=True, export_texcoords=True,
            export_animations=False, export_cameras=False, export_lights=False,
        )
        if "FINISHED" not in result:
            raise RuntimeError(f"Export échoué : {destination}")
        exported = exported_triangles(export)
        if exported != after:
            raise RuntimeError(f"Triangles exportés incohérents : {exported} au lieu de {after}.")
        # Le mode exclusif interdit aussi un écrasement si un fichier apparaît après le précontrôle.
        with export.open("rb") as src, destination.open("xb") as dst:
            shutil.copyfileobj(src, dst)
    print(f"  TRIANGLES AVANT/APRÈS : {before} -> {after} ; export={exported} ; {destination}", flush=True)
    if abs(after - target) > max(10, target * 0.10):
        print("  ATTENTION : écart à la cible > 10 % ; vérifier la topologie et le rendu.", flush=True)
    return before, after


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--merge-distance", type=float, default=0.00001,
                        help="Distance de fusion dans l'espace local du mesh ; défaut : 0.00001.")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    if not math.isfinite(args.merge_distance) or args.merge_distance < 0:
        parser.error("--merge-distance doit être une valeur positive ou nulle, finie.")
    repo = args.repo.resolve()
    models = repo / "Assets" / "_Plane" / "Models"
    counts = inventory_counts(repo / "docs" / "asset_inventory.md")
    plan = []
    # Contrôle de l'ensemble des entrées avant de générer le premier fichier.
    for folder, name, inventory_name, target, nominal, limit in TASKS:
        source = models / folder / f"{name}.glb"
        destination = source.with_name(f"{name}_lod0.glb")
        if not source.is_file():
            raise FileNotFoundError(source)
        if destination.exists():
            raise FileExistsError(f"Sortie déjà présente, aucun écrasement autorisé : {destination}")
        expected = counts[canonical(inventory_name)]
        if expected <= 0:
            raise ValueError(f"Triangles d'inventaire invalides : {inventory_name}")
        plan.append((source, destination, target, expected, nominal, limit))
    hashes = {source: sha256(source) for source, *_ in plan}
    try:
        for source, destination, target, expected, nominal, limit in plan:
            process(source, destination, target, expected, nominal, limit, args.merge_distance)
    finally:
        changed = [str(source) for source, checksum in hashes.items() if sha256(source) != checksum]
        if changed:
            raise RuntimeError("Sources modifiées : " + ", ".join(changed))
        print("\nIntégrité des sept GLB originaux vérifiée (SHA-256).", flush=True)
    print("Sept variantes exportées. Contrôler leur apparence dans Unity ; les prefabs existants restent sur les originaux.", flush=True)


if __name__ == "__main__":
    main()
