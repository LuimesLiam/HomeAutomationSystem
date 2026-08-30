FROM python:3.8-slim

# Set the working directory
WORKDIR /app

# Copy the current directory contents into the container at /app
COPY . /app

# Install system dependencies for numpy and other packages
RUN apt-get update \
    && apt-get install -y python3-pip supervisor libgl1-mesa-glx libglib2.0-0 libsm6 python3-numpy python3-scipy python3-dev gfortran libblas-dev liblapack-dev \
    && pip3 install -r PythonServices/requirements.txt --break-system-packages \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Expose the port the app runs on
EXPOSE 5000

# Define the command to run the app
CMD ["python3", "app.py"]