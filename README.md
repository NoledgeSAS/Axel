# AxelRagService

Service Web API .NET 10 (ASP.NET Core) qui expose des fonctionnalités de **RAG** (Retrieval Augmented Generation) basées sur **Azure AI Foundry / Azure AI Projects**. Le service s'appuie sur la librairie interne **Noledge.Global** pour la gestion de l'authentification, du chiffrement, de la journalisation et de l'accès à la base de données NeoConnect.

## Sommaire

- [Présentation](#présentation)
- [Prérequis](#prérequis)
- [Structure du projet](#structure-du-projet)
- [Paramétrage pour debug (appsettings.json)](#paramétrage-pour-debug-appsettingsjson)
- [Lancement en mode debug](#lancement-en-mode-debug)
- [Build d'une version de production](#build-dune-version-de-production)
- [Points d'API principaux](#points-dapi-principaux)

## Présentation

AxelRagService est un microservice REST destiné à être consommé par les applications clientes Axel. Il permet :

- L'identification d'un utilisateur (`/Identity/Identify`) à partir d'identifiants hashés et d'un domaine client.
- La création d'une nouvelle conversation avec un agent IA (`/Conversation/StartNewConversationAsync`).
- L'envoi d'un message dans une conversation existante (`/Conversation/SendMessageInConversation`).
- La récupération des conversations d'un utilisateur (`/Conversation/GetConversations`).
- L'historique d'une conversation (`/Conversation/GetConversationHistory`).
- La suppression d'une conversation (`/Conversation/DeleteConversation`).

Toutes les routes (sauf `/Identity/Identify`) sont protégées par un **token de session** passé dans l'en-tête HTTP `token`. Ce token est validé via `LoginSession.CheckLoginSession` qui s'appuie sur la base NeoConnect.

## Prérequis

- **.NET 10 SDK**
- **Visual Studio 2022 (17.10+)** ou **Visual Studio 2026** avec la charge de travail *Développement Web et ASP.NET*
- Accès à une **base NeoConnect** (chaîne de connexion SQL Server)
- Un projet **Azure AI Foundry** configuré côté Azure (endpoint, déploiement de modèle, index RAG)
- Un compte Windows avec droits d'écriture sur le dossier de logs configuré

## Structure du projet

```
AxelRagService/
├── Controllers/              # Endpoints REST
│   ├── ConversationController.cs
│   ├── CustomController.cs   # Contrôleur de base (auth, logs, config)
│   └── IdentityController.cs
├── Dto/                      # Objets de transfert
├── Providers/                # Logique métier (Conversation, IA)
├── Properties/launchSettings.json
├── Program.cs
├── appsettings.json
└── AxelRagService.csproj
```

## Paramétrage pour debug (appsettings.json)

Le service lit sa configuration via le système standard d'ASP.NET Core (`IConfiguration`). **Un fichier de configuration est utilisé : `appsettings.json`**, présent à la racine du projet et copié dans le dossier de sortie au moment du build.

> Le profil de lancement (`Properties/launchSettings.json`) positionne `ASPNETCORE_ENVIRONMENT=Development` en debug ; comme aucun fichier `appsettings.Development.json` n'est utilisé, c'est le contenu de `appsettings.json` qui est chargé quel que soit l'environnement. Pour basculer en production, il suffit de surcharger les valeurs nécessaires directement dans le `appsettings.json` déployé sur le serveur cible.

### Clés de configuration attendues

Le code (`CustomController.cs` et `ConversationController.cs`) consomme les clés suivantes :

| Clé                       | Type      | Obligatoire | Description                                                                 |
|---------------------------|-----------|-------------|-----------------------------------------------------------------------------|
| `NeoConnectCnx`           | string    | Oui         | Chaîne de connexion SQL Server à la base **NeoConnect**.                    |
| `IsEncryptedCnx`          | bool      | Oui         | Indique si `NeoConnectCnx` est chiffrée (SHA-512) ou en clair.             |
| `LogFolder`               | string    | Oui         | Chemin local du dossier dans lequel `WebLogHelper` écrit les logs.          |
| `Logging.LogLevel.Default`| string    | Non         | Niveau de log par défaut (`Information`, `Debug`, `Trace`...).              |
| `AllowedHosts`            | string    | Non         | Filtre des hôtes autorisés (laisser `*` en dev).                            |

### Exemple de `appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "NeoConnectCnx": "Server=localhost\\SQLEXPRESS;Database=NeoConnect;Trusted_Connection=True;TrustServerCertificate=True;",
  "IsEncryptedCnx": false,
  "LogFolder": "C:\\Logs\\AxelRagService"
}
```

### Configurer les secrets propres au poste

Pour éviter de committer des informations sensibles, il est recommandé d'utiliser les **secrets utilisateurs** de .NET :

```bash
dotnet user-secrets init
dotnet user-secrets set "NeoConnectCnx" "Server=...;Database=NeoConnect;..."
dotnet user-secrets set "LogFolder" "C:\\Dev\\Logs\\AxelRagService"
```

Les secrets sont stockés dans `%APPDATA%\\Microsoft\\UserSecrets\\<id>\\secrets.json` et surchargent `appsettings.json` **sans qu'il soit nécessaire de modifier le fichier lui-même**.

## Lancement en mode debug

### Via Visual Studio

1. Ouvrir la solution `AxelRagService.slnx` dans Visual Studio.
2. Vérifier que le projet **AxelRagService** est défini comme projet de démarrage (clic droit > *Définir comme projet de démarrage*).
3. Sélectionner le profil de lancement dans la barre d'outils :
   - **https** : `https://localhost:7296` (Swagger UI est alors ouvert automatiquement dans le navigateur à la racine).
   - **http**  : `http://localhost:5080`
   - **IIS Express** : `http://localhost:49908/`
4. Appuyer sur **F5** (ou *Déboguer > Démarrer le débogage*).

Le service expose alors Swagger à la racine (ex. `https://localhost:7296/`). La variable `ASPNETCORE_ENVIRONMENT` est automatiquement positionnée à `Development` par `Properties/launchSettings.json`.

### Via la ligne de commande

```bash
# Depuis le dossier contenant le .csproj
dotnet run --launch-profile https
```

Puis ouvrir `https://localhost:7296/` dans un navigateur pour accéder à Swagger.

### Vérification rapide

Une fois le service démarré, tester la disponibilité :

```bash
curl -k https://localhost:7296/swagger/v1/swagger.json
```

### Logs

Les logs applicatifs sont écrits par `Noledge.Global.Loggers.WebLogHelper` dans le dossier défini par `LogFolder`. Les logs ASP.NET Core standards apparaissent dans la console de Visual Studio / `dotnet run`.

## Build d'une version de production

### Pré-requis côté poste

- Configuration `appsettings.json` ciblant l'environnement de production (chaîne NeoConnect chiffrée, dossier de logs accessible au service, etc.).
- Une cible de publication configurée (dossier local, IIS, Azure App Service, conteneur Docker, etc.).

### Étapes dans Visual Studio

1. **Sélectionner la configuration Release**
   - Dans la barre d'outils, choisir `Release` à la place de `Debug`.

2. **Incrémenter le numéro de version**
   - Clic droit sur le projet **AxelRagService** > *Propriétés* > onglet *Package* (ou *Général* dans les versions récentes).
   - Dans le groupe **Version de l'assembly**, cliquer sur le bouton **Incrémenter la version**. Visual Studio ouvre la fenêtre *Informations sur l'assembly*.
   - Renseigner :
     - *Version principale* : incrément manuel (ex. `1` → `2`) lors d'une release majeure.
     - *Version secondaire* : incrément pour une nouvelle fonctionnalité compatible.
     - *Build* : laisser Visual Studio incrémenter automatiquement à chaque build (case à cocher *Incrémenter automatiquement la révision*).
     - *Révision* : généralement laissé à `0` ou auto.
   - Valider. Les champs `<Version>`, `<FileVersion>` et `<AssemblyVersion>` sont mis à jour dans le `.csproj` (vous pouvez aussi les saisir manuellement dans l'éditeur XML).
   - Exemple de résultat :
     ```xml
     <PropertyGroup>
       <TargetFramework>net10.0</TargetFramework>
       <Version>1.4.0</Version>
       <AssemblyVersion>1.4.0.0</AssemblyVersion>
       <FileVersion>1.4.0.0</FileVersion>
       <Nullable>enable</Nullable>
       <ImplicitUsings>enable</ImplicitUsings>
     </PropertyGroup>
     ```
   - Committer l'incrément dans le gestionnaire de sources en respectant la convention de messages du dépôt (ex. `chore(release): bump version to 1.4.0`).

3. **Publier le projet**
   - Clic droit sur le projet > *Publier* (ou *Générer > Publier la sélection*).
   - Choisir une **cible** :
     - **Dossier** : `bin\Release\net10.0\publish\` — utile pour un déploiement IIS ou copie manuelle.
     - **IIS, FTP, etc.** : cible prédéfinie du serveur cible.
     - **Azure App Service** : cible directe vers l'environnement cloud.
   - Cliquer sur **Publier**. Les binaires optimisés sont générés dans `bin\Release\net10.0\publish\`.

4. **Vérifier le livrable**
   - Contrôler la présence de `appsettings.json` dans le dossier publié et vérifier qu'il pointe sur l'environnement de production (chaîne NeoConnect chiffrée, `LogFolder` adapté, etc.).
   - Ajuster la variable d'environnement `ASPNETCORE_ENVIRONMENT=Production` sur le serveur cible si besoin.
   - Vérifier que le dossier `LogFolder` existe et est accessible en écriture par l'identité du service.

### Build en ligne de commande (équivalent)

```bash
dotnet build -c Release /p:Version=1.4.0 /p:AssemblyVersion=1.4.0.0 /p:FileVersion=1.4.0.0
dotnet publish -c Release -o ./publish /p:Version=1.4.0
```

## Points d'API principaux

| Méthode | Route                                              | Authentification | Description                                                 |
|---------|----------------------------------------------------|------------------|-------------------------------------------------------------|
| POST    | `/Identity/Identify`                               | Aucune           | Authentifie un utilisateur, retourne un token de session.   |
| POST    | `/Conversation/StartNewConversationAsync`          | Token            | Démarre une nouvelle conversation avec un agent IA.         |
| POST    | `/Conversation/SendMessageInConversation`          | Token            | Envoie un message dans une conversation existante.          |
| GET     | `/Conversation/GetConversations`                   | Token            | Liste les conversations de l'utilisateur courant.           |
| GET     | `/Conversation/GetConversationHistory?conversationId=` | Token         | Récupère l'historique de messages d'une conversation.       |
| DELETE  | `/Conversation/DeleteConversation?conversationId=` | Token            | Supprime une conversation.                                  |

Le détail du schéma JSON est consultable directement via Swagger UI une fois le service démarré.
