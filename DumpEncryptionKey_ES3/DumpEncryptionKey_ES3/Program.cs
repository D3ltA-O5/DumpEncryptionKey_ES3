using System;
using System.Collections.Generic;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

class Program
{
    static void Main()
    {
        // ----- измените пути под себя -------------
        string assetsPath = @"C:\Program Files (x86)\Steam\steamapps\common\Tiny Aquarium\Tiny Aquarium_Data\resources.assets";
        string classdataTpk = @"C:\Tools\classdata.tpk";
        // ------------------------------------------

        if (!File.Exists(assetsPath))
        {
            Console.WriteLine($"Файл не найден: {assetsPath}");
            return;
        }
        if (!File.Exists(classdataTpk))
        {
            Console.WriteLine($"classdata.tpk не найден: {classdataTpk}");
            return;
        }

        var am = new AssetsManager();
        am.LoadClassPackage(classdataTpk);

        // Загружаем .assets как экземпляр
        var fileInst = am.LoadAssetsFile(assetsPath, false);

        bool found = false;
        foreach (var info in fileInst.file.Metadata.AssetInfos)
        {
            // Получаем дерево полей
            var field = am.GetBaseField(fileInst, info);
            if (field == null || field.TypeName != "MonoBehaviour")
                continue;

            // Рекурсивный поиск encryptionKey
            if (TryFindKey(field, out string keyValue))
            {
                Console.WriteLine("✅ Найден encryptionKey:");
                Console.WriteLine($"   {keyValue}");
                found = true;
                // если нужен только первый — break;
            }
        }

        if (!found)
            Console.WriteLine("❌ Поле encryptionKey в resources.assets не найдено.");
    }

    // ---- Рекурсивный обход дерева AssetTypeValueField ----
    static bool TryFindKey(AssetTypeValueField node, out string value)
    {
        if (node.FieldName == "encryptionKey" && node.Value?.ValueType == AssetValueType.String)
        {
            value = node.AsString;
            return true;
        }

        foreach (var child in node.Children)
            if (TryFindKey(child, out value))
                return true;

        value = null;
        return false;
    }
}
