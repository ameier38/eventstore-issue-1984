# Event Store Issue #1984
ref: https://github.com/EventStore/EventStore/issues/1984

The goal is to make sure we can connect to Event Store use the .NET client
as well as connect to the admin UI. The main issue that I found was that
when trying to connect to the admin UI when running a multi-node cluster
is that the root endpoint will try to redirect to `/web/index.html` 
but with the internal IP address of the pod. As long as you put in the
fully qualified url with `/web/index.html` then it bypasses the redirect
and everything seems to work.

## Install Kubernetes
1. Install [multipass](https://multipass.run/)
2. Create an ssh key pair.
```
ssh-keygen
```
> Follow the prompts and note the key path
2. Rename `cloud-init.example.yaml`.
```
mv cloud-init.example.yaml cloud-init.yaml
```
3. Add the contents of the generated public key from step (2) into the `cloud-init.yaml` file.
```yaml
...
    ssh_authorized_keys:
      - <contents of your public key>
...
```
3. Launch an Ubuntu instance.
```
multipass launch --name es --cpus 2 --mem 2G --disk 20G --cloud-init
```
4. Mount this repository.
```
multipass mount . es:/repo
```
5. Get the instance IP address.
```
multipass info es
Name:           es
State:          Running
IPv4:           172.18.26.238
Release:        Ubuntu 18.04.3 LTS
Image hash:     babd5399b947 (Ubuntu 18.04 LTS)
Load:           0.13 0.05 0.01
Disk usage:     1.1G out of 4.7G
Memory usage:   487.7M out of 921.6M
Mounts:         C:/repos/github/ameier38/eventstore-issue-1984 => /repos/eventstore-issue-1984
                    UID map: -2:default
                    GID map: -2:default
```
4. Connect to the launched instance.
```
ssh -i /path/to/private-key-from-step-2 es@172.18.26.238
```
6. Install microk8s.
```
sudo snap install microk8s --classic
```
7. Add `es` user to microk8s group
```
sudo usermod -a -G microk8s es
```
> Disconnect and then reconnect for this to take effect
7. Check the status of Kubernetes
```
microk8s.status --wait-ready
```
8. Enable Helm and CoreDNS.
```
microk8s.enable helm dns
```
9. Initialize Helm.
```
microk8s.helm init
```
9. Add Event Store Helm repository
```
microk8s.helm repo add eventstore https://eventstore.github.io/EventStore.Charts
microk8s.helm repo update
```
10. Add Microsoft feeds.
```
wget -q https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
```
> ref: https://dotnet.microsoft.com/download/linux-package-manager/ubuntu18-04/sdk-current
11. Install .NET SDK
```
sudo add-apt-repository universe
sudo apt-get install apt-transport-https
sudo apt-get update
sudo apt-get install dotnet-sdk-2.2 -y
```
12. Install FAKE.
```
dotnet tool install fake-cli -g
```
> You will have to disconnect then reconnect for `fake` command to available.

## Single Node
1. Connect to the launched instance.
```
ssh -i /path/to/private-key-from-step-2 es@172.18.26.238 -L 2113:localhost:2113
```
2. Change to this repo directory.
```
cd /repo
```
3. Install Event Store.
```
microk8s.helm install -n eventstore eventstore/eventstore --set clusterSize=1
```
4. Check that Event Store is running.
```
microk8s.kubectl get pods -w
```
```
NAME                              READY   STATUS    RESTARTS   AGE
eventstore-0                      0/1     Running   0          8s
eventstore-admin-58d9c4ff-b9nlj   0/1     Running   0          8s
eventstore-0                      1/1     Running   0          29s
eventstore-admin-58d9c4ff-b9nlj   1/1     Running   0          37s
```
> Wait until both pods are ready (Ready = 1/1). Press `Ctrl-C` to stop watching.
5. Forward the port of the eventstore service.
```
microk8s.kubectl port-forward svc/eventstore 2113 &
microk8s.kubectl port-forward svc/eventstore 1113 &
```
6. Run the single node test.
```
fake build -t TestSingle
```
7. From your host machine enter `http://localhost:2113/web/index.html` in your browser and confirm
that you can see the Event Store admin page.

8. Clean up port-forward processes.
```
lsof -i
```
```
COMMAND   PID USER   FD   TYPE DEVICE SIZE/OFF NODE NAME
kubectl 27979   es    3u  IPv4 322278      0t0  TCP localhost:48036->localhost:16443 (ESTABLISHED)
kubectl 27979   es    5u  IPv4 322279      0t0  TCP localhost:48038->localhost:16443 (ESTABLISHED)
kubectl 27979   es    6u  IPv4 321418      0t0  TCP localhost:1113 (LISTEN)
kubectl 27979   es    7u  IPv6 321419      0t0  TCP ip6-localhost:1113 (LISTEN)
kubectl 32204   es    3u  IPv4 335001      0t0  TCP localhost:50290->localhost:16443 (ESTABLISHED)
kubectl 32204   es    5u  IPv4 334722      0t0  TCP localhost:50292->localhost:16443 (ESTABLISHED)
kubectl 32204   es    6u  IPv4 335009      0t0  TCP *:2113 (LISTEN)
kubectl 32204   es    7u  IPv4 362011      0t0  TCP localhost:2113->localhost:58942 (ESTABLISHED)
```
```
kill 32204
kill 27979
```
9. Delete the Event Store Helm release.
```
microk8s.helm delete eventstore --purge
```

## Multi Node
1. Change to this repo directory.
```
cd /repo
```
2. Install Event Store.
```
microk8s.helm install -n eventstore eventstore/eventstore --set clusterSize=3
```
3. Check that Event Store is running.
```
microk8s.kubectl get pods -w
```
```
NAME                              READY   STATUS    RESTARTS   AGE
eventstore-0                      0/1     Running   0          9s
eventstore-admin-58d9c4ff-tptjl   0/1     Running   0          9s
eventstore-0                      1/1     Running   0          28s
eventstore-1                      0/1     Pending   0          0s
eventstore-1                      0/1     Pending   0          0s
eventstore-1                      0/1     ContainerCreating   0          0s
eventstore-1                      0/1     Running             0          2s
eventstore-admin-58d9c4ff-tptjl   1/1     Running             0          36s
eventstore-1                      1/1     Running             0          24s
eventstore-2                      0/1     Pending             0          0s
eventstore-2                      0/1     Pending             0          1s
eventstore-2                      0/1     ContainerCreating   0          1s
eventstore-2                      0/1     Running             0          2s
eventstore-2                      1/1     Running             0          29s
```
4. Forward the port of the eventstore service.
```
microk8s.kubectl port-forward svc/eventstore 2113 &
```
5. Run the multi node test.
```
fake build -t TestMulti
```
6. From your host machine enter `http://localhost:2113/web/index.html` in your browser and confirm
that you can see the Event Store admin page.
> You must add the suffix `/web/index.html` otherwise you will get a redirect which
points to the private IP address of the pod since we are bound to `0.0.0.0` but advertising the pod IP.
