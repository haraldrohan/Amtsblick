// Amtsblick als Azure Container App: ein dauerhaft laufender Container hinter dem verwalteten
// HTTPS-Eingang der Plattform. Wird vom Release-Workflow bei jedem Tag angewendet.

@description('Region; Österreich ist für Container Apps verfügbar.')
param location string = 'austriaeast'

@description('Vollständiger Name des Container-Images samt Tag, z. B. ghcr.io/haraldrohan/amtsblick:0.1.0-beta.')
param image string

@description('Kontaktadresse des Betreibers für den User-Agent gegenüber den Datenquellen.')
param kontakt string

@description('Name, Anschrift und Kontakt des Betreibers für die Offenlegung auf der Startseite.')
param betreiber string = ''

@description('Eigene Domain ohne Schema, z. B. amtsblick.at. Leer: nur die Adresse der Plattform.')
param domain string = ''

var name = 'amtsblick'

// Ohne Log-Ziel: Die Konsolenausgabe ist nur im Live-Stream sichtbar und wird nicht aufbewahrt.
resource umgebung 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${name}-umgebung'
  location: location
  properties: {
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  properties: {
    environmentId: umgebung.id
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
    }
    template: {
      containers: [
        {
          name: name
          image: image
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            { name: 'Amtsblick__Kontakt', value: kontakt }
            { name: 'Amtsblick__Http__Betreiber', value: betreiber }
            // Der Eingang der Plattform reicht den Host-Header durch; angenommen werden nur die
            // eigene Domain und die Adresse der Plattform.
            {
              name: 'AllowedHosts'
              value: empty(domain) ? '*.${umgebung.properties.defaultDomain}' : '${domain};*.${umgebung.properties.defaultDomain}'
            }
          ]
        }
      ]
      // Genau eine Instanz: Cache, Kontingent und der eine eHYD-Abruf pro Stunde gelten je Instanz.
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

output adresse string = 'https://${app.properties.configuration.ingress.fqdn}'
