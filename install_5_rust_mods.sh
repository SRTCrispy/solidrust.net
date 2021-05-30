#!/bin/bash

sudo su - ${STEAMUSER}
pip3 install awscli
#aws configure
mkdir -p /root/.local/bin
echo "export PATH=\${PATH}:${HOME}/.local/bin" >> ".bashrc"
exit 

sudo su - ${STEAMUSER}

rm -rf /${HOME}/solidrust.net
mkdir -p /${HOME}/solidrust.net
aws s3 sync --only-show-errors --delete s3://solidrust.net-backups/repo /${HOME}/solidrust.net
chmod +x /${HOME}/solidrust.net/defaults/*.sh

${HOME}/solidrust.net/defaults/update_rust_service.sh

# Pull global env vars
source ${HOME}/solidrust.net/defaults/env_vars.sh
me=$(basename -- "$0")
echo "====> Starting ${me}: ${LOG_DATE}" | tee -a ${LOGS}

mkdir -p ${GAME_DIR}/server/solidrust/cfg
mkdir -p ${GAME_DIR}/oxide
ln -s ${HOME}/solidrust.net/defaults/solidrust.sh ${HOME}/.local/bin/solidrust.sh

aws s3 sync --quiet --delete ${S3_BACKUPS}/repo/defaults/oxide ${GAME_DIR}/oxide | tee -a ${LOGS}
aws s3 sync --quiet ${S3_BACKUPS}/repo/defaults/cfg ${GAME_DIR}/server/solidrust/cfg | tee -a ${LOGS}
mkdir -p ${GAME_DIR}/backup | tee -a ${LOGS}
sleep 2

aws s3 sync --quiet ${S3_BACKUPS}/repo/servers/${HOSTNAME}/server/solidrust/cfg ${GAME_DIR}/server/solidrust/cfg | tee -a ${LOGS}
aws s3 sync --quiet ${S3_BACKUPS}/repo/servers/${HOSTNAME}/oxide ${GAME_DIR}/oxide | tee -a ${LOGS}


# solidrust.sh &
netsh interface portproxy add v4tov4 listenaddress=0.0.0.0 listenport=28015 connectaddress=172.19.43.113 connectport=28015
netsh interface portproxy add v4tov4 listenaddress=0.0.0.0 listenport=28016 connectaddress=172.19.43.113 connectport=28016
netsh interface portproxy add v4tov4 listenaddress=0.0.0.0 listenport=28082 connectaddress=172.19.43.113 connectport=28082

netsh advfirewall firewall add rule name=”Open Port 28015 for WSL2” dir=in action=allow protocol=TCP localport=28015
netsh advfirewall firewall add rule name=”Open Port 28016 for WSL2” dir=in action=allow protocol=TCP localport=28016
netsh advfirewall firewall add rule name=”Open Port 28082 for WSL2” dir=in action=allow protocol=TCP localport=28082