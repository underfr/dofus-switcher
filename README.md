<div align="center">

<img src="https://raw.githubusercontent.com/underfr/dofus-switcher/assets/home.png" width="420" alt="Dofus Switcher"/>

# Dofus Switcher

**Outil de gestion multi-comptes pour Dofus - switch instantané entre fenêtres via hotkey**

![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D6?logo=windows)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![Standalone](https://img.shields.io/badge/exe-standalone-brightgreen)

</div>

---

## Fonctionnalités

- **Switch instantané** entre toutes tes fenêtres Dofus via un hotkey configurable (défaut : `Ctrl + Tab`)
- **Ordre de jeu personnalisable** - glisse/monte/descend les personnages dans la liste
- **Presets** - sauvegarde plusieurs configurations d'équipe avec une icône dédiée, charge-les en un clic
- **Détection automatique** des fenêtres Dofus ouvertes, mise à jour en temps réel
- **Genre & icône de classe** - choisit le bon sprite par personnage (masculin / féminin / symbole de classe)
- **Overlay flottant** - panneau compact draggable qui reste visible par-dessus le jeu, avec accès rapide aux presets et à la liste de l'équipe
- **Mémoire de session** - reprend le dernier preset utilisé et restaure genre/icône de chaque perso à la reconnexion
- **Exe autonome** - aucune installation, aucune dépendance externe

---

## Aperçu

| Fenêtre principale | Overlay réduit | Overlay développé |
|:-:|:-:|:-:|
| <img src="https://raw.githubusercontent.com/underfr/dofus-switcher/assets/home.png" width="300"/> | <img src="https://raw.githubusercontent.com/underfr/dofus-switcher/assets/overlay_close.png" width="180"/> | <img src="https://raw.githubusercontent.com/underfr/dofus-switcher/assets/overlay_open.png" width="180"/> |

---

## Utilisation

1. **Lance** `DofusSwitcher.exe`
2. **Ouvre** tes clients Dofus - ils apparaissent automatiquement dans la liste
3. **Ordonne** les personnages selon ton ordre de jeu (drag ou flèches ▲▼)
4. **Configure** le genre et l'icône de classe pour chaque perso
5. **Sauvegarde** un preset si tu veux retrouver cette configuration plus tard
6. **Appuie** sur `Ctrl + Tab` (ou ton hotkey choisi) pour cycler entre les fenêtres

> **Changer le hotkey** : clique sur le bouton en bas à gauche, appuie sur ta combinaison.

---

## Overlay

L'overlay flottant se positionne librement sur tes écrans. Il affiche le personnage actif et permet de :
- Passer directement à un personnage depuis la liste
- Changer l'ordre de jeu sans ouvrir la fenêtre principale
- Charger un preset en un clic

---

## Prérequis

- Windows 10 / 11 (x64)
- Dofus - version **3.x** (titres de fenêtre au format `Dofus X.X.X - NomPerso - Classe - Online`)

> L'exe est **standalone** : le runtime .NET 8 est embarqué, rien à installer.

---

## Installation

Télécharge `DofusSwitcher.exe` depuis les [Releases](../../releases), lance-le. C'est tout.

Les paramètres sont sauvegardés dans `%AppData%\DofusSwitcher\settings.json`.

> **SmartScreen / "éditeur inconnu"** : Windows peut afficher un avertissement au premier lancement car l'exe n'est pas signé par une autorité de certification commerciale. Clique sur **"Plus d'informations" → "Exécuter quand même"** pour continuer. L'avertissement disparaît une fois que l'exe a acquis suffisamment de téléchargements auprès de Microsoft.

---

## Compilation depuis les sources

```bash
git clone <repo>
cd DofusSwitcher
dotnet publish -c Release
# → bin/Release/net8.0-windows/win-x64/publish/DofusSwitcher.exe
```

Requiert [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
*Code signing provided by [SignPath Foundation](https://signpath.org/)*