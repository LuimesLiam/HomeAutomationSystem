#!/bin/bash

############################################################################
# configureDatabase.sh
# This script configures any speicific database scripts or settings that need
# to be run after the devcontainer is created. It is run as the vscode user
# so that it is configured with the correct permissions.
#
# Created by: Christopher Hair (MAK)
# Date: 2025-05-09
# Last updated: 2025-05-09
############################################################################

PostgresPassword=$1

echo "Configuring the database ..."