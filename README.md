# Password and Note Manager (PNM)

ENGLISH (EN-US)

Password and Note Manager (PNM) is a secure desktop application built with C# and WPF for managing encrypted passwords and notes locally, with backup and synchronization support via Google Drive and OneDrive.

---

## 💡 About the Project

**Password and Note Manager** securely stores your credentials and notes in an encrypted local database. To ensure data portability and security, the application allows you to sync backup files directly to your own cloud storage accounts (Google Drive and OneDrive) via OAuth 2.0.

Because this is an open-source project designed for local execution, **each user must use their own API credentials**, ensuring complete privacy and full control over their credentials.

---

## 🚀 Initial Setup (Google Cloud & Microsoft Azure)

To enable cloud synchronization, follow the step-by-step guides below to create your free access credentials.

### Part 1: Google Drive Setup

#### 1. Create a Google Cloud Project
1. Go to the [Google Cloud Console](https://console.cloud.google.com/).
2. Log in with your Google account.
3. At the top of the page, click the project selector dropdown and select **New Project**.
4. Enter a project name (e.g., `My Password Manager`) and click **Create**.

#### 2. Enable the Google Drive API
1. In the left sidebar navigation, go to **APIs & Services** > **Library**.
2. Search for `Google Drive API`.
3. Select the API and click **Enable**.

#### 3. Configure the OAuth Consent Screen
1. In the left sidebar, go to **APIs & Services** > **OAuth consent screen**.
2. Select **External** user type and click **Create**.
3. Fill in the required fields:
   - **App name:** `Password and Note Manager`
   - **User support email:** Your own email address.
   - **Developer contact information:** Your own email address.
4. Click **Save and Continue**.
5. In the **Scopes** tab, add the required Google Drive scopes (`.../auth/drive.file` or `.../auth/drive`).
6. In the **Test users** tab, click **+ Add Users** and enter the Google email address you will use to log in and store backups within the app.
7. Keep the **Publishing status** set to **Testing**.

#### 4. Create Application Credentials
1. In the left sidebar, click **APIs & Services** > **Credentials**.
2. At the top, click **+ Create Credentials** and select **OAuth client ID**.
3. In the **Application type** dropdown, choose **Desktop app**.
4. Set a name (e.g., `PNM Google Client`) and click **Create**.
5. A dialog will display your **Client ID** and **Client Secret**. Keep these keys safe!

---

### Part 2: Microsoft Azure Setup (OneDrive)

#### 1. Register a New Application in Azure Portal
1. Go to the [Microsoft Azure Portal](https://portal.azure.com/).
2. Search for and select **Microsoft Entra ID** (formerly Azure Active Directory).
3. In the left menu, select **App registrations** > **New registration**.
4. Fill in the application details:
   - **Name:** `Password and Note Manager`
   - **Supported account types:** Accounts in any organizational directory and personal Microsoft accounts (e.g., Skype, Xbox, Outlook.com).
   - **Redirect URI:** Select **Public client/native (mobile & desktop)** and enter `https://login.microsoftonline.com/common/oauth2/nativeclient`.
5. Click **Register**.

#### 2. Configure API Permissions (Microsoft Graph)
1. In your application's overview page, go to **API permissions** in the left sidebar.
2. Click **Add a permission** > **Microsoft Graph** > **Delegated permissions**.
3. Search for and select the following permissions:
   - `Files.ReadWrite` (or `Files.ReadWrite.AppFolder`)
   - `User.Read`
4. Click **Add permissions**.

#### 3. Copy your Client ID
1. Go back to the **Overview** menu of your application registration.
2. Copy the **Application (client) ID**. This is your `ONEDRIVE_CLIENT_ID`.

---

## 🛠️ Environment Variables Setup on Windows

The application retrieves credentials directly from your operating system environment variables. You need to register the required variables on your machine before running the application.

### Using Command Prompt (CMD) or PowerShell
Open your terminal and execute the commands below (replace the text inside quotes with your actual credentials):

```cmd
setx GOOGLE_CLIENT_ID "YOUR_GOOGLE_CLIENT_ID_HERE.apps.googleusercontent.com"
setx GOOGLE_CLIENT_SECRET "YOUR_GOOGLE_CLIENT_SECRET_HERE"
setx ONEDRIVE_CLIENT_ID "YOUR_AZURE_CLIENT_ID_HERE"




PORTUGUÊS (PT-BR)

O Password and Note Manager (PNM) é um aplicativo desktop seguro desenvolvido em C# e WPF para o gerenciamento local de senhas e notas criptografadas, com suporte a backup e sincronização via Google Drive e OneDrive.

---

## 💡 Sobre o Projeto

O **Password and Note Manager** armazena suas credenciais e anotações de forma segura em um banco de dados local criptografado. Para garantir a portabilidade e a segurança dos dados, o aplicativo permite sincronizar os arquivos de backup diretamente para as suas próprias contas de armazenamento em nuvem (Google Drive e OneDrive) via OAuth 2.0.

Por ser um projeto de código aberto concebido para execução local, **cada usuário deve utilizar suas próprias credenciais de API**, garantindo total privacidade e controle absoluto sobre as suas credenciais.

---

## 🚀 Configuração Inicial (Google Cloud e Microsoft Azure)

Para ativar a sincronização em nuvem, siga os guias passo a passo abaixo para criar suas credenciais de acesso gratuitas.

### Parte 1: Configuração do Google Drive

#### 1. Criar um Projeto no Google Cloud
1. Acesse o [Google Cloud Console](https://console.cloud.google.com/).
2. Faça login com a sua conta Google.
3. No topo da página, clique no menu suspenso de seleção de projetos e selecione **Novo Projeto**.
4. Digite um nome para o projeto (por exemplo, `My Password Manager`) e clique em **Criar**.

#### 2. Ativar a API do Google Drive
1. Na barra lateral esquerda, acesse **APIs e Serviços** > **Biblioteca**.
2. Pesquise por `Google Drive API`.
3. Selecione a API e clique em **Ativar**.

#### 3. Configurar a Tela de Consentimento OAuth
1. Na barra lateral esquerda, acesse **APIs e Serviços** > **Tela de consentimento OAuth**.
2. Selecione o tipo de usuário **Externo** e clique em **Criar**.
3. Preencha os campos obrigatórios:
   - **Nome do app:** `Password and Note Manager`
   - **E-mail de suporte do usuário:** Seu próprio endereço de e-mail.
   - **Informações de contato do desenvolvedor:** Seu próprio endereço de e-mail.
4. Clique em **Salvar e Continuar**.
5. Na aba **Escopos**, adicione os escopos necessários do Google Drive (`.../auth/drive.file` ou `.../auth/drive`).
6. Na aba **Usuários de teste**, clique em **+ Adicionar Usuários** e digite o endereço de e-mail do Google que você usará para fazer login e armazenar backups no aplicativo.
7. Mantenha o **Status de publicação** definido como **Em teste**.

#### 4. Criar Credenciais do Aplicativo
1. Na barra lateral esquerda, clique em **APIs e Serviços** > **Credenciais**.
2. No topo, clique em **+ Criar Credenciais** e selecione **ID do cliente OAuth**.
3. No menu suspenso **Tipo de aplicativo**, escolha **Aplicativo para computador**.
4. Defina um nome (por exemplo, `PNM Google Client`) e clique em **Criar**.
5. Uma janela exibirá seu **ID de Cliente** e sua **Chave Secreta de Cliente**. Mantenha essas chaves seguras!

---

### Parte 2: Configuração do Microsoft Azure (OneDrive)

#### 1. Registrar um Novo Aplicativo no Portal do Azure
1. Acesse o [Portal do Microsoft Azure](https://portal.azure.com/).
2. Pesquise e selecione **Microsoft Entra ID** (antigo Azure Active Directory).
3. No menu esquerdo, selecione **Registros de aplicativo** > **Novo registro**.
4. Preencha os detalhes da aplicação:
   - **Nome:** `Password and Note Manager`
   - **Tipos de conta compatíveis:** Contas em qualquer diretório organizacional e contas pessoais da Microsoft (por exemplo, Skype, Xbox, Outlook.com).
   - **URI de redirecionamento:** Selecione **Cliente público/nativo (celular e desktop)** e insira `https://login.microsoftonline.com/common/oauth2/nativeclient`.
5. Clique em **Registrar**.

#### 2. Configurar as Permissões da API (Microsoft Graph)
1. Na página de visão geral do seu aplicativo, acesse **Permissões de API** na barra lateral esquerda.
2. Clique em **Adicionar uma permissão** > **Microsoft Graph** > **Permissões delegadas**.
3. Pesquise e selecione as seguintes permissões:
   - `Files.ReadWrite` (ou `Files.ReadWrite.AppFolder`)
   - `User.Read`
4. Clique em **Adicionar permissões**.

#### 3. Copiar seu ID de Cliente
1. Volte ao menu **Visão geral** do seu registro de aplicativo.
2. Copie o **ID do aplicativo (cliente)**. Esta é a sua variável `ONEDRIVE_CLIENT_ID`.

---

## 🛠️ Configuração de Variáveis de Ambiente no Windows

O aplicativo obtém as credenciais diretamente das variáveis de ambiente do seu sistema operacional. Você precisa registrar as variáveis necessárias na sua máquina antes de executar o aplicativo.

### Utilizando o Prompt de Comando (CMD) ou PowerShell
Abra o seu terminal e execute os comandos abaixo (substitua o texto entre aspas pelas suas credenciais reais):

```cmd
setx GOOGLE_CLIENT_ID "SEU_GOOGLE_CLIENT_ID_AQUI.apps.googleusercontent.com"
setx GOOGLE_CLIENT_SECRET "SUA_CHAVE_SECRETA_GOOGLE_AQUI"
setx ONEDRIVE_CLIENT_ID "SEU_AZURE_CLIENT_ID_AQUI"