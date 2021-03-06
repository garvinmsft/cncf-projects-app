
Refer the [yml](yml) folder for the yamls

- [Please find Source Code here](src)

# Setup

Create a Kubernetes Cluster

Install Nginx Ingress controller https://docs.microsoft.com/en-us/azure/aks/ingress-basic#create-an-ingress-controller
Following commands from the first section of the referenced Docs Link is needed. 

```
# Create a namespace for your ingress resources
kubectl create namespace ingress-basic

# Add the ingress-nginx repository
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx

# Use Helm to deploy an NGINX ingress controller
helm install nginx-ingress ingress-nginx/ingress-nginx \
    --version 3.23.0 \
    --namespace ingress-basic \
    --set controller.replicaCount=2 \
    --set controller.nodeSelector."beta\.kubernetes\.io/os"=linux \
    --set defaultBackend.nodeSelector."beta\.kubernetes\.io/os"=linux \
    --set controller.admissionWebhooks.patch.nodeSelector."beta\.kubernetes\.io/os"=linux
```
Set the variable to be used as the top level domain for this exercise. Use a custom domain or a cloud service provided domain name.
```
topLevelDomain=desiredhostnamename.com
```

If using AKS, a DNS name label can be assigend to the public IP of the Loadbalancer
 - Open the Public IP resource associated with the EXTERNAL-IP address of the LoadBalancer service
 - Navigate to the Configuration blade and set a unique name in the DNS name label
 - Use the FQDN. For ex. uniquename.centralus.cloudapp.azure.com

## Rook Installation

```
kubectl apply -f yml/rook-common.yaml
kubectl apply -f yml/rook-operator.yaml
kubectl apply -f yml/rook-cluster.yaml
kubectl apply -f yml/rook-storageclass.yaml
```

## Harbor Installation

Install Ingress for Harbor.
```
# Create namespace for the harbor nginx ingress controller 
kubectl create namespace harbor-ingress-system

# Add the nginx helm repo
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx

# Install nginx ingress for Harbor
helm install harbor-nginx-ingress ingress-nginx/ingress-nginx \
    --namespace harbor-ingress-system \
    --set controller.ingressClass=harbor-nginx \
    --set controller.replicaCount=2 \
    --set controller.nodeSelector."beta\.kubernetes\.io/os"=linux \
    --set defaultBackend.nodeSelector."beta\.kubernetes\.io/os"=linux

# Label the ingress-basic namespace to disable cert resource validation
kubectl label namespace harbor-ingress-system cert-manager.io/disable-validation=true
```

Install Cert Manager
```
# Label the ingress-basic namespace to disable resource validation
kubectl label namespace ingress-basic cert-manager.io/disable-validation=true

# Add the Jetstack Helm repository
helm repo add jetstack https://charts.jetstack.io

# Update your local Helm chart repository cache
helm repo update

# Install the cert-manager Helm chart
helm install \
  cert-manager \
  --namespace ingress-basic \
  --version v0.16.1 \
  --set installCRDs=true \
  --set nodeSelector."beta\.kubernetes\.io/os"=linux \
  jetstack/cert-manager
```

Create the ClusterIssuer by applying the below YAML with the email address changed

```
cat <<EOF | kubectl create -f -
apiVersion: cert-manager.io/v1alpha2
kind: ClusterIssuer
metadata:
  name: letsencrypt
  namespace: ingress-basic
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: MY_EMAIL_ADDRESS
    privateKeySecretRef:
      name: letsencrypt
    solvers:
    - http01:
        ingress:
          class: harbor-nginx
          podTemplate:
            spec:
              nodeSelector:
                "kubernetes.io/os": linux      
EOF
```

Retrieve the public IP of the Loadbalancer service
```
kubectl get svc -n harbor-ingress-system
```

Assign a DNS label to the Ingress Public IP and update it for the registryHost variable

If using AKS, a DNS name label can be assigend to the public IP of the Loadbalancer created in the harbor-ingress-system namespace.
 - Open the Public IP resource associated with the EXTERNAL-IP address of the LoadBalancer service for harbor ingress
 - Navigate to the Configuration blade and set a unique name in the DNS name label
 - Use the FQDN. For ex. uniquenameforharboringress.centralus.cloudapp.azure.com

```
registryHost={FQDN DNS label Name to be updated here}
externalUrl=https://$registryHost

# Create the namespace for harbor installation
kubectl create namespace harbor-system
# Add the harbor helm repo 
helm repo add harbor https://helm.goharbor.io

# Install Harbor
helm install harbor harbor/harbor \
	--namespace harbor-system \
	--version 1.6.0 \
	--set expose.ingress.hosts.core=$registryHost \
	--set expose.tls.secretName=ingress-cert-harbor \
	--set notary.enabled=false \
	--set trivy.enabled=false \
	--set expose.ingress.annotations."kubernetes\.io/ingress\.class"=harbor-nginx \
	--set expose.ingress.annotations."cert-manager\.io/cluster-issuer"=letsencrypt  \
	--set persistence.enabled=true \
	--set externalURL=$externalUrl \
	--set harborAdminPassword=admin \
	--set persistence.persistentVolumeClaim.registry.storageClass=rook-ceph-block \
	--set persistence.persistentVolumeClaim.chartmuseum.storageClass=rook-ceph-block \
	--set persistence.persistentVolumeClaim.jobservice.storageClass=rook-ceph-block \
	--set persistence.persistentVolumeClaim.database.storageClass=rook-ceph-block
  

```
Patch the database stateful for the Harbor database so it will not error on pod restarts
```
kubectl patch statefulset harbor-harbor-database -n harbor-system --patch "$(cat yml/harbor-init-patch.yaml)"
```
Confirm harber installed and running then create the harbor project and user
```bash
#Create conexp project in Harbor
 curl -u admin:admin -i -k -X POST "$externalUrl/api/v2.0/projects" \
      -d "@json/harbor-project.json" \
      -H "Content-Type: application/json"

#Create conexp user in Harbor
 curl -u admin:admin -i -k -X POST "$externalUrl/api/v2.0/users" \
      -d "@json/harbor-project-user.json" \
      -H "Content-Type: application/json"

#Add the conexp user to the conexp project in Harbor

conexpid=$(curl -u admin:admin -k -s -X GET "$externalUrl/api/v2.0/projects?name=conexp" | jq '.[0].project_id')
echo "project_id: $conexpid"

 curl -u admin:admin -i -k -X POST "$externalUrl/api/v2.0/projects/$conexpid/members" \
      -d "@json/harbor-project-member.json" \
      -H "Content-Type: application/json"
```
Now retrieve the Harbor Registry URL:
```bash 
echo $externalUrl
```
Use the following credentials to login:\
admin\
admin

## Database Installation - 
```bash
#Deploy Vitess
kubectl create ns vitess-system
kubectl apply -f yml/vitess_operator.yaml -n vitess-system
kubectl apply -f yml/vitess_cluster.yaml -n vitess-system

#vgate host is random so create known service
kubectl apply -f yml/mysql-host-service.yaml -n vitess-system
```

## OpenFaaS

```bash
helm repo add openfaas https://openfaas.github.io/faas-netes/
helm repo update

kubectl apply -f https://raw.githubusercontent.com/openfaas/faas-netes/master/namespaces.yml

kubectl -n openfaas create secret generic basic-auth --from-literal=basic-auth-user=admin --from-literal=basic-auth-password="FTA@CNCF0n@zure3"

helm install openfaas openfaas/openfaas -f yml/openfaas-values.yaml -n openfaas
```

```
kubectl port-forward deploy/gateway 8080:8080 -n openfaas

Browse to http://localhost:8080 and use the username/password as admin/FTA@CNCF0n@zure3
```

Install the Nats Connector
```
kubectl apply -f yml/openfaas-nats-connector.yaml
```
## Prometheus

```
kubectl create ns monitoring
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo update
helm install prometheus prometheus-community/kube-prometheus-stack -f yml/prometheus-values.yaml  \
  -n monitoring \
  --version 13.13.0
```
```
kubectl port-forward deploy/prometheus-grafana 8080:3000 -n monitoring
Browse to http://localhost:8080 and use the username/password as admin/FTA@CNCF0n@zure3

kubectl port-forward svc/prometheus-kube-prometheus-prometheus 9090:9090 -n monitoring 
Browse to http://localhost:9090
```

## Jaeger

```
helm repo add jaegertracing https://jaegertracing.github.io/helm-charts
helm repo update

kubectl create ns tracing
helm install jaeger jaegertracing/jaeger -f yml/jaeger-values.yaml \
  -n tracing \
  --version 0.40.1
```
```
# Wait for at least ~5 minutes before browsing to the Jaeger UI
kubectl port-forward svc/jaeger-query 8080:80 -n tracing
Browse to http://localhost:8080
```

## Linkerd

Deploy Linkered
```
# Install cli
curl -sL https://run.linkerd.io/install | sh
export PATH=$PATH:$HOME/.linkerd2/bin
linkerd version
linkerd check --pre

# Generate certificates.
wget https://github.com/smallstep/cli/releases/download/v0.15.2/step-cli_0.15.2_amd64.deb
​sudo dpkg -i step-cli_0.15.2_amd64.deb

step certificate create identity.linkerd.cluster.local ca.crt ca.key --profile root-ca --no-password --insecure
step certificate create identity.linkerd.cluster.local issuer.crt issuer.key --ca ca.crt --ca-key ca.key --profile intermediate-ca --not-after 8760h --no-password --insecure

# Install linkerd
linkerd install --identity-trust-anchors-file ca.crt --identity-issuer-certificate-file issuer.crt --identity-issuer-key-file issuer.key | kubectl apply -f -
```

Integrate Openfaas with Linkerd (need to wait for Linker do to come up)
```
kubectl -n openfaas get deploy gateway -o yaml | linkerd inject --skip-outbound-ports=4222 - | kubectl apply -f -
```

Integrate Nginx Ingress controller with Linkerd
```
kubectl get deploy/nginx-ingress-ingress-nginx-controller -n ingress-basic -o yaml | linkerd inject - | kubectl apply -f - 
```

Linkerd metrics integration with Prometheus
```
kubectl create secret generic additional-scrape-configs --from-file=yml/linkerd-prometheus-additional.yaml -n monitoring
kubectl edit prometheus  prometheus-prometheus-oper-prometheus  -n monitoring

Add the additionalScrapeConfigs as below
  ....
  ....
  serviceMonitorSelector:
    matchLabels:
      team: frontend
  additionalScrapeConfigs:
    name: additional-scrape-configs
    key: linkerd-prometheus-additional.yaml
  ....
  ....
```

Linkerd integration with Jaeger
```
kubectl  apply -f yml/linkerd-opencesus-collector.yaml -n tracing

kubectl annotate namespace openfaas-fn config.linkerd.io/trace-collector=oc-collector.tracing:55678
kubectl annotate namespace openfaas config.linkerd.io/trace-collector=oc-collector.tracing:55678
kubectl annotate namespace ingress-basic config.linkerd.io/trace-collector=oc-collector.tracing:55678
```

```
kubectl port-forward svc/linkerd-web 8080:8084 -n linkerd
Browse to http://localhost:8080
```

## Tekton
Install Tekton pipelines
```
kubectl apply -f https://storage.googleapis.com/tekton-releases/pipeline/previous/v0.21.0/release.yaml

kubectl apply -f yml/tekton-default-configmap.yaml  -n  tekton-pipelines
kubectl apply -f yml/tekton-pvc-configmap.yaml -n  tekton-pipelines
kubectl apply -f yml/tekton-feature-flags-configmap.yaml -n  tekton-pipelines
```
Install Tekton Triggers
```
kubectl apply --filename https://storage.googleapis.com/tekton-releases/triggers/previous/v0.11.2/release.yaml
```
Install Tekton Dashboard
```
kubectl apply --filename https://github.com/tektoncd/dashboard/releases/download/v0.14.0/tekton-dashboard-release.yaml
```
```
kubectl port-forward svc/tekton-dashboard 8080:9097  -n tekton-pipelines
Browse to http://localhost:8080
```

## App Installation

Build and push the containers
```
docker login $registryHost
conexp
FTA@CNCF0n@zure3

#Build and push API service
docker build -t $registryHost/conexp/api:latest src/Contoso.Expenses.API
docker push $registryHost/conexp/api:latest

#Build and push web app
docker build -t $registryHost/conexp/web:latest  -f src/Contoso.Expenses.Web/Dockerfile ./src
docker push $registryHost/conexp/web:latest

#Build and push email dispatcher
docker build -t $registryHost/conexp/emaildispatcher:latest  -f src/Contoso.Expenses.OpenFaaS/Dockerfile ./src
docker push $registryHost/conexp/emaildispatcher:latest
```

```
kubectl create ns conexp-mvp
kubectl annotate namespace conexp-mvp linkerd.io/inject=enabled
kubectl annotate namespace conexp-mvp config.linkerd.io/skip-outbound-ports="4222"
kubectl annotate namespace conexp-mvp config.linkerd.io/trace-collector=oc-collector.tracing:55678
```

Create the registry credentials in teh deployment namespaces
```
kubectl create secret docker-registry regcred --docker-server="https://$registryHost" --docker-username=conexp  --docker-password=FTA@CNCF0n@zure3  --docker-email=user@mycompany.com -n conexp-mvp
kubectl create secret docker-registry regcred --docker-server="https://$registryHost" --docker-username=conexp  --docker-password=FTA@CNCF0n@zure3  --docker-email=user@mycompany.com -n openfaas-fn
```

## Tekton - App Deployment

```
kubectl create ns conexp-mvp-devops

kubectl apply -f yml/app-webhook-role.yaml -n conexp-mvp-devops
kubectl apply -f yml/app-admin-role.yaml -n conexp-mvp-devops

kubectl apply -f yml/app-create-ingress.yaml -n conexp-mvp-devops
kubectl apply -f yml/app-create-webhook.yaml -n conexp-mvp-devops
```

Update Secret (basic-user-pass) for registry credentails, TriggerBinding for registry name,namespaces in triggers.yaml
Create a SendGrid Account and set an API key for use
```
sendGridApiKey=<<set the api key>>
appHostName=$topLevelDomain

sed -i "s/{registryHost}/$registryHost/g" yml/app-triggers.yaml

sed -i "s/{SENDGRIDAPIKEYRELACE}/$sendGridApiKey/g" yml/app-pipeline.yaml
sed -i "s/{APPHOSTNAMEREPLACE}/$appHostName/g" yml/app-pipeline.yaml

kubectl apply -f yml/app-pipeline.yaml -n conexp-mvp-devops
kubectl apply -f yml/app-triggers.yaml -n conexp-mvp-devops
```

Roles and bindings in the deployment namespace
```
kubectl apply -f yml/app-deploy-rolebinding.yaml -n conexp-mvp
kubectl apply -f yml/app-deploy-rolebinding.yaml -n openfaas-fn
```

Generate PAT token(Settings->Developer settings->Personal access tokens) for the repo -> public_repo, admin:repo_hook, set the pat token below
```
patToken=<<set the pat tokne>>

sed -i "s/{patToken}/$patToken/g" yml/app-github-secret.yaml

kubectl apply -f yml/app-github-secret.yaml -n conexp-mvp-devops
```

set org/user/repo of the source code repo variables below
```
cicdWebhookHost=$topLevelDomain

gitHubOrg=<<set the name of the github org>>
gitHubUser=<<set the name of the github user>>
gitHubRepo=<<set the name of the github repo>>

sed -i "s/{cicdWebhook}/$cicdWebhookHost/g" yml/app-ingress-run.yaml

kubectl apply -f yml/app-ingress-run.yaml  -n conexp-mvp-devops

sed -i "s/{cicdWebhook}/$cicdWebhookHost/g" yml/app-webhook-run.yaml
sed -i "s/{mygithub-org-replace}/$gitHubOrg/g" yml/app-webhook-run.yaml
sed -i "s/{mygithub-user-replace}/$gitHubUser/g" yml/app-webhook-run.yaml
sed -i "s/{mygithub-repo-replace}/$gitHubRepo/g" yml/app-webhook-run.yaml

kubectl apply -f yml/app-webhook-run.yaml -n conexp-mvp-devops
```
