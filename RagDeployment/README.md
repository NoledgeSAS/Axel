# RagDeployment

## But du repo


Ce dépôt a pour but de contenir un kit de déploiement permettant de créer et déployer simplement l'infrastructure Azure nécessaire à la mise en place d'un agent IA RAG pour un nouveau client.

L'objectif est de garantir :

- une architecture Azure identique et reproductible pour chaque client ;
- une diminution des erreurs humaines lors des déploiements ;
- une réduction du temps nécessaire à la création d'un nouvel environnement ;
- un suivi clair des ressources et des consommations Azure par client.

Chaque client dispose de son propre environnement Azure isolé dans un Resource Group dédié afin de permettre :
- un suivi précis des coûts ;
- une isolation des données ;
- une personnalisation de l'environnement IA selon les besoins du client.


## Prérequis


- Azure CLI
    ``` shell
    winget install --exact --id Microsoft.AzureCLI
    ```
- PowerShell
- Bicep CLI

Le compte Azure utilisé pour le déploiement doit disposer des permissions nécessaires pour créer les ressources suivantes :

- Resource Group
- Azure Storage Account
- Azure AI Search
- Azure AI Foundry
- Azure AI Foundry Project
- Déploiements de modèles IA

## Utilisation

La création d'un nouvel environnement client se fait en plusieurs étapes :

1. Renseigner les paramètres du client dans le fichier : `deploy/parameters/customer.json`
2. Exécuter le script principal : `deploy/Deploy-NewCustomer.ps1`


Ce script orchestre la création complète de l'environnement Azure.

Il se charge notamment de :

- vérifier les prérequis ;
- générer les noms des ressources Azure selon les conventions définies ;
- lancer les déploiements Bicep ;
- créer les ressources nécessaires au fonctionnement de l'agent IA.

Lorsque l'exécution du script est terminé, vous aurez besoin de divers informations à récupérer dans les resources créées afin de remplir les `COM_PARAMS` du client et permettre l'utilisation.
- `IA_INDEX_NAME`: Dans le [Portail Azure](https://portal.azure.com/#servicemenu/Microsoft_Azure_Resources/ResourceManager/resourcegroups), aller dans le resource group créé et ouvrir le searchservice. Dans `Indexes` vous trouverez le nom de l'index
- `IA_FOUNDRY_ENDPOINT`: Dans [Foundry (new)](https://ai.azure.com/nextgen/r/,-,,-/allresources?tid=21f0f1a5-e73b-4ae3-af3e-21ad0ac89a4a), aller dans le foundry project créé, prendre la valeur contenue dans `Point de terminaison de projet`
- `IA_MODEL_DEPLOYMENT_NAME` : Dans [Foundry (new)](https://ai.azure.com/nextgen/r/,-,,-/allresources?tid=21f0f1a5-e73b-4ae3-af3e-21ad0ac89a4a), aller dans le foundry project créé, aller dans l'onglet `Build` > `Modèles` et prendre le nom du modèle gpt (pas le text embeding)
- `IA_PROJECT_CONNECTION_ID`: Dans [Foundry (new)](https://ai.azure.com/nextgen/r/,-,,-/allresources?tid=21f0f1a5-e73b-4ae3-af3e-21ad0ac89a4a), aller dans le foundry project créé, aller dans l'onglet `Build` > `Outils` et ouvrir la `xxxxxx-search-service-noledge-connection` prendre la valeur de `ID de connexion du projet`
- `IA_TITLE_AGENT_NAME` : Dans [Foundry (new)](https://ai.azure.com/nextgen/r/,-,,-/allresources?tid=21f0f1a5-e73b-4ae3-af3e-21ad0ac89a4a), aller dans le foundry project créé, aller dans l'onglet `Build` > `Agents` et prendre le nom du `xxx-title-agent`
- `IA_AGENT_INSTRUCTION`: Cette valeur peut être ammenée à être customisée en fonction des tests avec le client, et en fonction de ses attentes. La valeur par défault est :
    ``` Text 
    Agis en tant qu'assistant commercial spécialisé dans le secteur des spiritueux, avec une parfaite connaissance des méthodes marketing (notamment la méthode des 4P : Produit, Prix, Place, Promotion) et une rigueur absolue en analyse documentaire.

    Ton objectif est d’aider un commercial de Pernod Ricard à répondre précisément aux questions terrain en t’appuyant EXCLUSIVEMENT sur la base documentaire à ta disposition. La base documentaire est mise à ta disposition via un tool Azure AI Search.
    Tu recherchera dans cet outil afin de trouver toute information utile te permettant de répondre à l'utilisateur.

    Tâche : Répondre à la question du commercial en utilisant uniquement les informations présentes dans la base documentaire, en structurant ta réponse selon la méthode des 4P seulement lorsque cela est pertinent.

    Exigences obligatoires :

    1) Tu dois uniquement utiliser les informations contenues dans la base documentaire (tool Azure AI Search).
    2) Tu ne dois jamais ajouter d’information issue de tes connaissances générales.  
    3) Chaque affirmation doit être explicitement reliée à un document.  
    4) Si l’information n’est pas présente dans la base documentaire, tu dois répondre explicitement :
    “Information non disponible dans la base documentaire.”
    Tu n'a le droit de répondre cela seulement après avoir effectué un tool de recherche dans la base documentaire et ne pas avoir trouvé d’information pertinente.

    Méthodologie (raisonnement étape par étape) :

    Étape 1 : Identifier précisément la question posée.  
    Étape 2 : Parcourir mentalement la base documentaire et extraire uniquement les éléments pertinents.  
    Étape 3 : Rédiger une réponse claire, structurée et exploitable par un commercial terrain.  
    Étape 4 : Ajouter les références précises des documents utilisés après chaque section ou affirmation clé.

    Contraintes de format :

    - Structure claire avec titres distincts pour chaque P lorsque applicable.  
    - Style professionnel, synthétique et orienté action.  
    - Aucune supposition.  
    - Aucune reformulation interprétative non fondée.  
    - Aucune information sans source explicitement mentionnée.  

    Avant de finaliser, vérifie que :
    - Toutes les affirmations sont sourcées.  
    - Aucun contenu externe aux documents n’a été ajouté.  
    - La méthode des 4P a utiliser seulement si pertinent.  
    ```

## Structure des scripts

Le script principal est :  `deploy/Deploy-NewCustomer.ps1` 

C'est le point d'entrée du déploiement.

Il orchestre la création de l'architecture Azure nécessaire en utilisant les fichiers `.bicep` contenus dans : `deploy/bicep`

Les fichiers `.bicep` permettent de décrire sous forme de code les ressources Azure à créer.

Cette approche permet de garantir que chaque environnement client est créé de manière identique et reproductible.

Organisation prévue :
```
deploy
│
├── Deploy-NewCustomer.ps1
│
├── parameters
│   └── customer.json
│
└── bicep
    ├── resource-group.bicep
    ├── storage.bicep
    ├── search.bicep
    ├── foundry.bicep
    └── ...
```



## Conventions de nommage

Chaque environnement client utilise une convention de nommage permettant d'identifier facilement les ressources Azure associées.

La convention générale est :
```
{client}-{type-de-resource}
```
Exception : les Resource Groups utilisent le préfixe `RG_IA` afin d'être facilement identifiables et regroupés dans Azure.

Exemple pour un client nommé `Contoso` :

| Ressource | Nom |
|---|---|
| Resource Group | RG_IA_Contoso |
| Azure AI Search | Contoso-search-service |
| Index | Contoso-index |
| Indexer | Contoso-indexer |
| Datasource | Contoso-datasource |
| Skillset | Contoso-skillset |
| Azure AI Foundry | Contoso-foundry |
| Foundry Project | Contoso-foundry-project |
| Storage Account | contosostorageaccount |
| Blob Container | Contoso-blob-container |


Certaines ressources Azure possèdent des contraintes spécifiques de nommage.
Le script de déploiement adapte automatiquement les noms lorsque cela est nécessaire (par exemple pour les Storage Accounts qui nécessitent uniquement des caractères minuscules et une longueur limitée).


## Architecture cible

Chaque client possède un environnement Azure indépendant :
```
RG_IA_Client
│
├── Storage Account
│ └── Blob Container contenant les documents
│
├── Azure AI Search
│ ├── Datasource
│ ├── Indexer
│ ├── Index
│ └── Skillset
│
├── Azure AI Foundry
│ └── Foundry Project
│ └── Agent IA
│
└── Model Deployment
└── Modèle GPT utilisé par l'agent
```