packer {
  required_plugins {
    amazon = {
      source  = "github.com/hashicorp/amazon"
      version = ">= 1.3.0"
    }
  }
}

source "amazon-ebs" "slowroad" {
  region        = "eu-north-1"
  instance_type = "t3.micro"
  ssh_username  = "ec2-user"
  ami_name      = "slowroad-base-{{timestamp}}"

  source_ami_filter {
    filters = {
      name = "al2023-ami-2023*-x86_64"
    }
    owners      = ["amazon"]
    most_recent = true
  }
}

build {
  sources = ["source.amazon-ebs.slowroad"]

  provisioner "shell" {
    inline = [
      "sudo dnf install -y docker",
      "sudo systemctl enable docker",
      "sudo usermod -aG docker ec2-user",
      "sudo mkdir -p /usr/local/lib/docker/cli-plugins",
      "sudo curl -SL https://github.com/docker/compose/releases/download/v2.29.7/docker-compose-linux-x86_64 -o /usr/local/lib/docker/cli-plugins/docker-compose",
      "sudo chmod +x /usr/local/lib/docker/cli-plugins/docker-compose",
    ]
  }
}
