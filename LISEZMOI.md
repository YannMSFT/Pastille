# Pastille 0.5.2 — webcam flottante pour Windows 11 ARM64 et x64

Application native WPF/.NET 8, sans navigateur embarqué, serveur ni FFmpeg.
Un seul fichier **Pastille.exe**, autonome : le runtime .NET Desktop correspondant est inclus.
La webcam reste sur votre PC. Aucun accès réseau n'est prévu par l'application.
Cette version ne comporte plus d'enregistrement, de capture d'écran ni d'accès au microphone.
Les anciennes vidéos ne sont ni modifiées ni supprimées.

## Utilisation

1. Lancez `Pastille.exe`. L'application démarre dans la **zone de notification**, près de
   l'horloge, sans ouvrir de fenêtre de configuration ni ajouter de bouton à la barre des tâches.
   Son icône représente une pastille blanche octogonale verticale, légèrement biseautée,
   sur fond transparent, sans inscription ni marque. Si Windows la masque,
   ouvrez le menu **^** des icônes cachées. Cliquez sur l'icône, ou faites un clic droit
   puis **Configuration…**. Choisissez une webcam, puis **Activer la webcam**.
2. Glissez la pastille pour la déplacer. Réglez sa largeur et sa hauteur indépendamment,
   avec les curseurs ou sa poignée inférieure. Choisissez **Ovale** ou **Rectangle**.
   Une largeur et une hauteur identiques donnent un cercle.
3. Dans **Liseré**, activez ou désactivez **Afficher le liseré**, réglez son épaisseur
   de 1 à 20 unités d'affichage et cliquez sur **Choisir la couleur…** pour choisir
   une couleur personnalisée dans le sélecteur Windows. Les changements sont immédiats,
   pour les deux formes. Les paramètres sont conservés lorsque le liseré est désactivé.
   Juste après le liseré, **Arrondi du rectangle** permet de régler le rayon des coins
   de 0 à 200 unités d'affichage : **0 donne des angles droits**. Ce réglage est disponible
   uniquement en mode **Rectangle**, avec ou sans liseré. L'image et son liseré suivent le
   même arrondi, limité à la moitié du plus petit côté de la pastille.
   Le rayon est conservé lorsque vous passez en mode Ovale puis revenez en Rectangle.
4. Dans **Cadrage du visage**, réglez le **Zoom** de 50 % à 400 % et les curseurs
   **Horizontal** / **Vertical** pour centrer votre visage à l'intérieur de la pastille.
   Vers la droite, le curseur horizontal déplace l'image vers la droite ; le curseur vertical
   déplace l'image vers le bas. La molette sur la pastille règle également le zoom.
   **Réinitialiser le cadrage** restaure le zoom à 100 % et centre l'image, sans déplacer
   ni redimensionner la pastille. **Recentrer la pastille** déplace la fenêtre sur l'écran.
5. **Ctrl + Maj + F9** cache ou réaffiche immédiatement la pastille depuis n'importe quelle
   application, même lorsque la configuration est fermée. Le bouton d'affichage
   et la case **Afficher la pastille** permettent la même action. Le clic droit propose
   aussi **Cacher la pastille**. La webcam reste active pendant que la pastille est cachée :
   utilisez **Arrêter la webcam** pour libérer la caméra.

Le clic droit sur l'icône de la zone de notification propose **Configuration…**,
**Cacher / Afficher la pastille** et **Quitter Pastille**. Un clic sur l'icône ouvre aussi
la configuration ; le clavier (Entrée/Espace) permet également de l'ouvrir.

Fermer la configuration avec la croix, le bouton **Fermer la configuration** ou la réduire
la masque simplement dans la zone de notification. La pastille et la webcam restent dans
leur état actuel. Pour **arrêter complètement l'application et libérer la webcam**,
utilisez **Quitter Pastille** depuis l'icône. L'icône et le raccourci global sont alors libérés.

Double-cliquez sur la pastille pour retrouver les commandes. Fermez la configuration et
masquez la pastille avec le raccourci pour montrer tout l'écran. Le raccourci agit uniquement
sur la pastille, sans afficher ni activer la configuration. Les autres réglages ne sont pas modifiés.

La position, la taille, la forme, le miroir, le zoom, le cadrage, le liseré, l'arrondi et la webcam sont mémorisés.
La webcam ne s'active pas au lancement. La pastille est visible au lancement.
Le cadrage par défaut remplit la pastille sans déformer l'image.
Un dézoom inférieur à 100 % ou un décalage important peut laisser apparaître son fond sombre.

## Installation et mise à jour

Quittez l'ancienne version (Visage ou Pastille) depuis son icône avant de lancer Pastille 0.5.2.
Placez **Pastille.exe** dans le dossier de votre choix et double-cliquez dessus.
L'exécutable est le seul fichier à copier : ni DLL, ni fichier de configuration livré à côté,
ni installation de .NET ne sont nécessaires. Le guide et les sources sont facultatifs.

Choisissez l'archive ZIP correspondant à votre PC :

- **Pastille-x64-0.5.2.zip** : processeur Intel ou AMD 64 bits.
- **Pastille-ARM64-0.5.2.zip** : processeur ARM64, notamment Snapdragon.

Dans **Paramètres > Système > Informations système > Type du système**, Windows indique
le type de processeur. Les deux archives contiennent uniquement **Pastille.exe** :
clic droit sur l'archive, puis **Extraire tout**. Ne lancez pas l'application depuis le ZIP.

Le résultat est un seul **Pastille.exe** (environ 70 à 80 Mo). L'archive n'est plus nécessaire
après extraction. Il n'y a pas d'installateur.

Au premier lancement, si Pastille n'a pas encore de préférences, les réglages de
`%LOCALAPPDATA%\Visage\settings.json` sont importés dans
`%LOCALAPPDATA%\Pastille\settings.json`. Le fichier de Visage est conservé sans modification.
Ensuite, les préférences Pastille sont prioritaires. Les anciens paramètres d'enregistrement
sont ignorés. Aucun changement de vos vidéos existantes.

Le runtime inclus rend l'exécutable plus volumineux. Au démarrage, .NET extrait
automatiquement ses bibliothèques natives dans son cache `%TEMP%\.net\Pastille`.
Les bibliothèques embarquées sont compressées, ce qui peut légèrement allonger le démarrage.
Il faut donc pouvoir écrire dans le dossier temporaire de l'utilisateur.
Cela ne demande aucune manipulation et ne crée pas de DLL à côté de Pastille.exe.
Les préférences et journaux restent dans `%LOCALAPPDATA%\Pastille` : « fichier unique »
concerne la distribution, pas l'absence de fichiers de travail lors de l'exécution.

Les versions sont destinées à **Windows 11 ARM64** ou **Windows 11 x64 (Intel/AMD)**.
Utilisez de préférence la version native de votre processeur. Il n'y a pas de version x86 32 bits.
Pas de droits administrateur requis. Le binaire n'est pas signé ; Windows peut afficher
un avertissement SmartScreen. Ne contournez pas les règles de sécurité d'un PC géré.
Les correctifs du runtime inclus sont apportés en remplaçant l'exécutable par une nouvelle
version publiée ; ils ne proviennent pas du runtime .NET installé séparément.

## Limites

- La pastille reste devant les fenêtres classiques, pas devant les écrans de sécurité Windows
  ni nécessairement devant les applications plein écran exclusif.
- Le visage n'est pas détecté ni détouré automatiquement : utilisez les réglages de cadrage
  si vous n'êtes pas au centre du flux webcam.
- Les autorisations se règlent dans **Paramètres > Confidentialité et sécurité > Caméra >
  Autoriser les applications de bureau**.
- Si le raccourci est déjà utilisé (notamment par une ancienne instance de Visage ou Pastille), un message
  apparaît dans les commandes. Fermez l'application en conflit puis relancez Pastille.
  Le bouton et la case d'affichage restent utilisables. Le maintien des touches ne fait
  pas clignoter la pastille : la répétition du raccourci est désactivée.
- L'icône est réinscrite si l'Explorateur Windows redémarre. Si Windows refuse de créer
  l'icône au démarrage, Pastille affiche une erreur et s'arrête pour ne pas rester inaccessible.
- L'aperçu webcam est limité à 960 × 720 et environ 25 images/s ; son tampon est réutilisé.
  Masquer la pastille ne suspend pas la webcam, pour permettre un réaffichage immédiat.
- Préférences et journal d'erreurs : `%LOCALAPPDATA%\Pastille`.

## Compilation et vérification

Avec le SDK .NET 8 pour Windows :

```powershell
dotnet publish .\Pastille.csproj -c Release -r win-arm64 -o .\publish-arm64
dotnet publish .\Pastille.csproj -c Release -r win-x64 -o .\publish-x64
```

Le projet utilise `win-arm64` par défaut, surchargeable par `-r win-x64`, et configure `SelfContained`, `PublishSingleFile`,
`IncludeNativeLibrariesForSelfExtract`, la compression des bibliothèques embarquées et des symboles embarqués. Le dossier publié
contient uniquement **Pastille.exe**. Le trimming est désactivé pour préserver WPF/WinRT.

```powershell
.\Pastille.exe --self-test
```

Les vérifications portent sur l'architecture ciblée (ARM64 ou x64), les préférences et leur compatibilité,
le centrage, le zoom, le cadrage, les deux formes, le miroir, le redimensionnement,
le rendu du liseré (activation, couleurs et épaisseurs), et le masquage/réaffichage
via le message Windows du raccourci, les commandes et le menu. Elles contrôlent également
le rayon des coins, le découpage de l'image et du liseré, l'indépendance du mode Ovale,
l'inscription réelle de l'icône dans Windows, le démarrage sans configuration, la fermeture
vers la zone de notification et la suppression de l'icône à la sortie.
Le résultat est écrit dans `self-test-results.txt` à côté de l'exécutable ;
un échec produit `self-test-error.txt` et un code de sortie non nul.
Ce mode n'active pas la webcam, ne modifie pas vos préférences et ne produit pas de vidéo.
Le dessin du systray est vérifié de 16 à 64 pixels et un aperçu de l'icône est écrit dans
`pastille-icon-preview.png` à côté de l'exécutable en mode test uniquement.
Il vérifie aussi le nom Pastille et l'import des anciennes préférences dans des dossiers
temporaires isolés. Le binaire publié est également vérifié avec son moteur CoreCLR intégré,
sa bibliothèque de base embarquée et ses dépendances WPF natives extraites, sans dépendre
de l'installation .NET du PC.

Références : [WinRT depuis une application de bureau](https://learn.microsoft.com/windows/apps/desktop/modernize/desktop-to-uwp-enhance),
[MediaFrameReader](https://learn.microsoft.com/windows/uwp/audio-video-camera/process-media-frames-with-mediaframereader),
[raccourcis globaux Windows](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-registerhotkey),
[zone de notification Windows](https://learn.microsoft.com/windows/win32/api/shellapi/nf-shellapi-shell_notifyiconw).

Publication : [exécutable unique .NET](https://learn.microsoft.com/dotnet/core/deploying/single-file/overview).
