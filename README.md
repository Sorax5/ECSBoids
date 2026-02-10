# ECS Boids Simulation

Ce projet est une implémentation haute performance de l'algorithme de **Boids** (simulation de nuée d'oiseaux ou de bancs de poissons) utilisant **Unity ECS (Entity Component System)** et **DOTS (Data-Oriented Technology Stack)**.

Grâce à l'architecture ECS et au compilateur Burst, cette simulation peut gérer des milliers d'entités simultanément tout en maintenant un taux de rafraîchissement élevé.

[![Démo YouTube](http://img.youtube.com/vi/thuwFoeI_6s/0.jpg)](https://youtu.be/thuwFoeI_6s)

## Fonctionnalités

- **Simulation de Boids** : Implémentation des trois règles fondamentales de Reynolds :
  - **Séparation** : Éviter les collisions avec les voisins proches.
  - **Alignement** : S'aligner avec la direction moyenne des voisins.
  - **Cohésion** : Se diriger vers le centre de masse du groupe local.
- **Performance DOTS** : Construit entièrement sur Unity Entities, Burst Compiler et C# Job System.
- **Optimisation Spatiale** : Utilise une grille spatiale (Uniform Grid / HashMap) pour optimiser la recherche de voisins, permettant un grand nombre d'agents.
- **Confinement** : Les boids restent confinés dans une zone de simulation définie (`SpawnZone`).
- **Configuration Dynamique** : Paramètres ajustables via des objets scriptables (`BoidsDefinition`) pour modifier le comportement en temps réel (force, rayon, vitesse, angle de vue).
- **Système de Profils** : Différents profils de comportement pré-configurés (ex: Aligné, Non-aligné, Suivre).
- **Caméra Libre** : Script de caméra libre (`FreeFlyCamera`) pour naviguer dans la scène.

## Structure du Projet

- `Assets/Scripts/Components` : Définition des données ECS par composants (ex: `BoidComponent`).
- `Assets/Scripts/System` : Logique de la simulation.
  - `BoidSteeringSystem.cs` : Calcul des forces de direction (Séparation, Alignement, Cohésion) avec partitionnement spatial.
  - `BoidMovementSystem.cs` : Application des mouvements basés sur la vélocité.
  - `BoidAreaConfinementSystem.cs` : Maintient les boids dans la zone de jeu.
- `Assets/Scripts/BoidSpawner.cs` : Script "Authoring" (MonoBehaviour) pour configurer et instancier les entités depuis l'éditeur.
- `Assets/Data` : Contient les `ScriptableObjects` de configuration (`BoidsDefinition`).

## Installation et Utilisation

1.  Ouvrez le projet dans **Unity**.
2.  Ouvrez la scène `Assets/Scenes/SampleScene.unity`.
3.  Lancez le mode **Play**.
4.  Utilisez les touches (selon la configuration de `FreeFlyCamera`) pour vous déplacer dans la scène pour observer la simulation.

### Configuration

Vous pouvez modifier le comportement de la simulation en sélectionnant les assets de configuration dans `Assets/Data` ou en modifiant le `BoidSpawner` dans la scène.

Les paramètres clefs dans `BoidsDefinition` incluent :

- **Cohesion** : Force et rayon pour rester groupé.
- **Alignment** : Force et rayon pour aller dans la même direction.
- **Separation** : Force et rayon pour éviter les collisions.
- **Boid Parameter** : Vitesse et angle de vue (Field of View).
