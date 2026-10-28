# DeskPoint / Equipe em Ponto — C# + PostgreSQL

Esta versão foi convertida para **ASP.NET Core MVC (C# / .NET 10)** com **PostgreSQL + Entity Framework Core**. A interface foi recriada em HTML/CSS/Razor para manter a identidade visual do projeto revisado sem depender de React, TypeScript, Vite ou Supabase.

## Funcionalidades incluídas
- Página inicial com relógio em tempo real e último registro de ponto.
- Login administrativo por e-mail e senha.
- Painel do chefe com contadores, cadastro de até cinco funcionários ativos, associação de UID RFID, desativação sem apagar histórico, cadastro/remoção/conclusão de tarefas e últimos 100 registros.
- Área do funcionário por código de acesso, com tarefas e histórico de ponto.
- Endpoint para o ESP32 registrar ponto por UID RFID.
- Datas gravadas em UTC para evitar o erro PostgreSQL `DateTimeOffset` com offset `-03:00`.

## Requisitos
- .NET 10 SDK
- PostgreSQL instalado e iniciado

## Configurar
1. Crie um banco PostgreSQL chamado `equipe_em_ponto` (ou ajuste a connection string em `appsettings.json`).
2. Edite `ConnectionStrings:Postgres` e substitua `COLOQUE_SUA_SENHA` pela senha local do PostgreSQL. Não compartilhe essa senha.
3. Configure uma senha de administrador pela variável de ambiente no PowerShell:

```powershell
$env:Admin__Email="chefe@deskpoint.local"
$env:Admin__Password="EscolhaUmaSenhaForte123!"
$env:Rfid__ApiKey="crie-uma-chave-longa-e-privada"
```

A primeira execução cria as tabelas em um banco vazio e cadastra o administrador se ainda não houver um. O projeto usa `EnsureCreated`; se você já tem tabelas antigas com esquema diferente, faça backup e use um banco de teste novo para evitar divergências.

## Executar
Abra o PowerShell na pasta que contém `EquipeEmPonto.csproj` e rode:

```powershell
dotnet restore
dotnet build
dotnet run
```

Abra a URL indicada no terminal. Para esta versão, se o terminal mostrar `http://localhost:5000`, acesse `http://localhost:5000`. Deixe o terminal aberto enquanto usa o sistema. Se a porta estiver ocupada, pare a instância anterior ou encerre o processo que a utiliza antes de iniciar outra.

## Login e funcionário
- Área do chefe: botão “Área do chefe”.
- Área do funcionário: solicite o código de seis caracteres gerado ao cadastrar a pessoa; ele aparece no painel do chefe.
- A senha do administrador só é criada automaticamente quando a tabela de administradores está vazia.

## Endpoint ESP32 / RFID
`POST /api/ponto/rfid` com JSON `{ "cardUid": "A1B2C3D4" }` e cabeçalho `X-Device-Key` igual à variável `Rfid__ApiKey`. Cadastre o UID no painel do chefe. Para acessar de outro dispositivo na rede, use o IP local do computador em vez de `localhost`.

## Observações
- O ZIP não inclui senha de banco nem chave RFID reais.
- Teste primeiro com banco de desenvolvimento.
- A integração RFID requer teste com o ESP32 físico e a mesma rede Wi-Fi.
- Esta é uma versão acadêmica/protótipo, não um sistema de ponto comercial certificado.
