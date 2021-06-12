#!/bin/bash
## Global default environments

# automation lock
export SOLID_LCK="${HOME}/SolidRusT.lock"
# default root of where the game server is installed
export GAME_ROOT="/game"
# construct default server logging endpoint
export SERVER_LOGS="${GAME_ROOT}/RustDedicated.log"
# Amazon s3 destination for backups
export S3_BACKUPS="s3://solidrust.net-backups"
# Amazon s3 destination for backups
export S3_WEB="s3://solidrust.net"
# Github source for configs
export GITHUB_ROOT="${HOME}/solidrust.net"
# Default configs
export SERVER_GLOBAL="${GITHUB_ROOT}/defaults"
# Customized config
export SERVER_CUSTOM="${GITHUB_ROOT}/servers/${HOSTNAME}"
# local RCON CLI config
export RCON_CFG="${GITHUB_ROOT}/defaults/rcon.yaml"
# logging format
export LOG_DATE=$(date +"%Y_%m_%d_%I_%M_%p")
# log file location
export LOG_FILE="SolidRusT.log"
# construct full log output endpoint
export LOGS="${HOME}/${LOG_FILE}"
# instantiate the log file
touch ${LOGS}
# Build root
export BUILD_ROOT="${HOME}/build-solidrust"

# end of env_vars
echo "++++++= Initialized SolidRusT =++++++" | tee -a ${LOGS}