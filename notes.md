# Projet FTP - Multithread
---

###Serveur
---

Il permet de prendre en charge les demandes des clients.
 
 **Commandes serveur :**
 
 * Ajout d'utilisateurs : ajoute un user dans le fichier conf xml
 * Kick de connexion : kill le thread du client
 * logs : ouvre le fichier texte des logs
 * lister les connexions : affiche les clients connectés et leur adresses ip
 * Arret du serveur
 
 **Commandes FTP (un thread par cmd):**
 
 * Authentification a partir du fichier conf xml
 * Upload de fichier
 * Download de fichier
 * Suppression de fichier
 * Creation de dossier
 * Suppression de dossier
 * ...
 * **Toutes les cmd FTP sauf : AUTH (FTP over TLS), NLST, SITE, STAT, HELP, SMNT, REST, ABOR**
 
**Un thread par connexion cliente : ThreadPool**
 
 
 
###Client
---

Il permet de sélectionner les fichiers à envoyer/recevoir et suivre l'avancée dutransfert.

**Interface graphique :**

* Authentification
* Visualisation de l'arborescence de fichiers client et serveur
* Transfert de fichier et annulation de transfert
* Suivi de la progression d'un tranfert
* Suppression de fichier
* Creation de dossier
* Log des commandes de la session

**Un thread par cmd : ThreadPool, limite de 10 executions simultanées**
 
 