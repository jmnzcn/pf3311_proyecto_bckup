# Azure — PF-3311 (paridad con `_tools/aws/`)

## Requisitos

1. Azure CLI: `winget install Microsoft.AzureCLI`
2. Login: `az login` (o código de dispositivo)
3. Cuota GPU en **eastus**: `Standard NCASv3 Family` (mín. 12 vCPUs para 3× `NC4as_T4_v3`)

## Orden de ejecución

```powershell
cd _tools\azure

# 1) Login (una vez; abre navegador o código de dispositivo)
az login

# 2) Crear RG, NSG, 3 VMs GPU, storage
.\provision_pf3311.ps1

# 3) Instalar NICE DCV en las 3 VMs
.\install_dcv_all.ps1

# 4) Build local (Unity cerrado) + deploy
cd ..\_tools
.\build_windows.bat
cd azure
.\deploy_build.ps1
```

## URLs

Tras provisionar, ver `_tools/azure/pf3311_instances.json`:

- `https://<IP>:8443` — DCV en navegador
- Usuario: `pf3311admin` (contraseña en `pf3311_vm_credentials.json`, no commitear)

## Apagar VMs (ahorro)

```powershell
az vm deallocate --resource-group pf3311-rg --name pf3311-vm1
# repetir vm2, vm3
```

## Encender de nuevo

```powershell
az vm start --resource-group pf3311-rg --name pf3311-vm1
```
